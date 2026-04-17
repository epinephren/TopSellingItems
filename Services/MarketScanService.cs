using Dalamud.Plugin.Services;
using TopSellingItems.Models;

namespace TopSellingItems.Services;

public sealed class MarketScanService
{
    private readonly IPluginLog log;
    private readonly Configuration configuration;
    private readonly UniversalisClient universalisClient;
    private readonly ItemScanner itemScanner;
    private readonly WorldService worldService;

    private const int RequestDelayMs = 120;

    public MarketScanService(
        IPluginLog log,
        Configuration configuration,
        UniversalisClient universalisClient,
        ItemScanner itemScanner,
        WorldService worldService)
    {
        this.log = log;
        this.configuration = configuration;
        this.universalisClient = universalisClient;
        this.itemScanner = itemScanner;
        this.worldService = worldService;
    }

    public async Task<List<CrossDcOpportunityRow>> ScanCrossDcAsync(
        Action<string>? setStatus,
        CancellationToken cancellationToken)
    {
        var itemIds = this.itemScanner.GetCandidateItemIds();

        if (this.configuration.ScanItemLimit > 0 && itemIds.Count > this.configuration.ScanItemLimit)
        {
            var random = Random.Shared;
            var shuffled = itemIds.ToList();

            for (var i = shuffled.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            itemIds = shuffled.Take(this.configuration.ScanItemLimit).ToList();
        }

        var rows = new List<CrossDcOpportunityRow>();

        const int batchSize = 100;
        var totalBatches = (int)Math.Ceiling(itemIds.Count / (double)batchSize);

        var homeWorld = this.worldService.GetWorldById(this.configuration.HomeWorldId);
        if (homeWorld is null)
            throw new InvalidOperationException("Home world is not set.");

        var selectedWorld = this.worldService.GetWorldById(this.configuration.SelectedWorldId);
        if (selectedWorld is null)
            throw new InvalidOperationException("Selected world is not set.");

        var homeScope = this.configuration.SellOnHomeDatacenter
            ? homeWorld.DatacenterName
            : homeWorld.WorldName;

        List<string> scanScopes;
        if (this.configuration.ScanAllDatacenters)
        {
            var homeDatacenterId = homeWorld.DatacenterId;

            scanScopes = this.worldService.GetDatacenterIds()
                .Where(id => id != 0)
                .Where(id => id != homeDatacenterId)
                .Select(this.worldService.GetDatacenterName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            var selectedScope = this.configuration.UseDatacenterScope
                ? selectedWorld.DatacenterName
                : selectedWorld.WorldName;

            if (string.IsNullOrWhiteSpace(selectedScope))
                throw new InvalidOperationException("Selected datacenter/world is not set.");

            scanScopes = new List<string> { selectedScope };
        }

        if (scanScopes.Count == 0)
            throw new InvalidOperationException("No scan scopes available.");

        for (var i = 0; i < itemIds.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchNumber = (i / batchSize) + 1;
            var batch = itemIds.Skip(i).Take(batchSize).ToArray();

            setStatus?.Invoke($"Scanning batch {batchNumber}/{totalBatches}...");
            this.log.Info(
                $"TopSellingItems: cross-DC batch {batchNumber}/{totalBatches}, home={homeScope}, items={batch.Length}");

            List<UniversalisHistoryItem> homeHistory;
            try
            {
                homeHistory = await this.universalisClient.GetHistoryAsync(
                    homeScope,
                    batch,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                this.log.Error(ex,
                    $"TopSellingItems: failed to fetch home history for batch {batchNumber}/{totalBatches}");
                throw;
            }

            var homeHistoryByItem = homeHistory.ToDictionary(x => x.ItemId, x => x);

            var foreignCurrentByScope =
                new Dictionary<string, Dictionary<uint, UniversalisCurrentItem>>(StringComparer.OrdinalIgnoreCase);

            foreach (var scope in scanScopes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var currentData = await this.universalisClient.GetCurrentDataAsync(
                        scope,
                        batch,
                        cancellationToken);

                    foreignCurrentByScope[scope] = currentData.ToDictionary(x => x.ItemId, x => x);
                }
                catch (Exception ex)
                {
                    this.log.Warning(ex,
                        $"TopSellingItems: skipping scope {scope} for batch {batchNumber}/{totalBatches}");
                }

                await Task.Delay(RequestDelayMs, cancellationToken);
            }

            var cutoff = DateTimeOffset.UtcNow.AddDays(-this.configuration.HistoryDays);

            foreach (var itemId in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!homeHistoryByItem.TryGetValue(itemId, out var homeEntry))
                    continue;

                var sales = homeEntry.RecentHistory
                    .Where(s => s.SoldAt is not null && s.SoldAt.Value >= cutoff)
                    .Where(s => this.configuration.IncludeHq || !s.Hq)
                    .ToList();

                if (sales.Count == 0)
                    continue;

                var soldUnits = sales.Sum(s => s.Quantity);
                if (soldUnits < this.configuration.MinSalesCount)
                    continue;

                var salesPerDay = soldUnits / (double)this.configuration.HistoryDays;
                if (salesPerDay < this.configuration.MinDailySales)
                    continue;

                var homeAverageSalePrice = MedianPrice(sales);
                if (homeAverageSalePrice <= 0)
                    continue;

                var homeMinListingPrice = homeEntry.MinListingPrice > 0
                    ? homeEntry.MinListingPrice
                    : sales.Min(s => s.PricePerUnit);

                UniversalisCurrentItem? bestBuy = null;

                foreach (var scopeData in foreignCurrentByScope.Values)
                {
                    if (!scopeData.TryGetValue(itemId, out var current))
                        continue;

                    if (current.MinListingPrice <= 0)
                        continue;

                    if (bestBuy is null || current.MinListingPrice < bestBuy.MinListingPrice)
                        bestBuy = current;
                }

                if (bestBuy is null)
                    continue;

                var estimatedProfit = homeAverageSalePrice - bestBuy.MinListingPrice;
                if (estimatedProfit < this.configuration.MinAbsoluteProfit)
                    continue;

                var profitPercent = estimatedProfit / bestBuy.MinListingPrice;
                if (profitPercent < this.configuration.MinProfitPercent)
                    continue;

                var routeScore = estimatedProfit * salesPerDay;

                rows.Add(new CrossDcOpportunityRow
                {
                    ItemId = itemId,
                    Name = this.itemScanner.GetItemName(itemId),
                    HomeScope = homeScope,
                    BestBuyScope = bestBuy.ScopeName,
                    SalesPerDay = salesPerDay,
                    HomeAverageSalePrice = homeAverageSalePrice,
                    HomeMinListingPrice = homeMinListingPrice,
                    BestBuyPrice = bestBuy.MinListingPrice,
                    EstimatedProfit = estimatedProfit,
                    ProfitPercent = profitPercent,
                    RouteScore = routeScore
                });
            }
        }

        var ordered = rows
            .OrderByDescending(r => r.RouteScore)
            .ThenByDescending(r => r.ProfitPercent)
            .ThenByDescending(r => r.SalesPerDay)
            .Take(this.configuration.TopN)
            .ToList();

        this.log.Info($"TopSellingItems: cross-DC scan complete, results={ordered.Count}");
        setStatus?.Invoke($"Done. {ordered.Count} cross-DC results.");

        return ordered;
    }

    private static double MedianPrice(List<UniversalisSale> sales)
    {
        if (sales.Count == 0)
            return 0;

        var ordered = sales
            .Select(s => s.PricePerUnit)
            .OrderBy(x => x)
            .ToList();

        var mid = ordered.Count / 2;

        return ordered.Count % 2 == 0
            ? (ordered[mid - 1] + ordered[mid]) / 2.0
            : ordered[mid];
    }
}