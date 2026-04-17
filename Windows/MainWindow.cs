using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Globalization;
using System.Numerics;
using TopSellingItems.Models;
using TopSellingItems.Services;

namespace TopSellingItems.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly MarketScanService marketScanService;
    private readonly WorldService worldService;

    private CancellationTokenSource? cts;
    private bool isScanning;
    private string status = "Idle";
    private List<CrossDcOpportunityRow> rows = new();

    public MainWindow(
        Configuration configuration,
        MarketScanService marketScanService,
        WorldService worldService)
        : base("Top Selling Items###TopSellingItemsMain")
    {
        this.configuration = configuration;
        this.marketScanService = marketScanService;
        this.worldService = worldService;

        this.Size = new Vector2(1250, 700) * ImGuiHelpers.GlobalScale;
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.RespectCloseHotkey = true;
    }

    public override void Draw()
    {
        this.DrawScanScopeControls();
        ImGui.Separator();

        this.DrawHomeScopeControls();
        ImGui.Separator();

        if (ImGui.CollapsingHeader("Scan settings##ScanSettings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            this.DrawScanControls();
        }

        ImGui.Separator();
        this.DrawResultsTable();
    }

    private void DrawScanScopeControls()
    {
        ImGui.TextUnformatted("Scan scope");

        var useDc = this.configuration.UseDatacenterScope;
        if (ImGui.Checkbox("Use datacenter scope", ref useDc))
        {
            this.configuration.UseDatacenterScope = useDc;
        }

        ImGui.SameLine();

        var scanAllDcs = this.configuration.ScanAllDatacenters;
        if (ImGui.Checkbox("Scan all datacenters", ref scanAllDcs))
        {
            this.configuration.ScanAllDatacenters = scanAllDcs;
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.85f, 0.2f, 1f)))
        {
            ImGui.TextUnformatted("(Warning: Scanning ALL Datacenters may take several minutes! Also includes JP/US)");
        }

        this.DrawWorldSelector(
            "Selected Datacenter",
            "Selected World",
            this.configuration.SelectedWorldId,
            worldId => this.configuration.SelectedWorldId = worldId);
    }

    private void DrawHomeScopeControls()
    {
        ImGui.TextUnformatted("Home sell scope");

        var sellOnHomeDc = this.configuration.SellOnHomeDatacenter;
        if (ImGui.Checkbox("Sell on home datacenter", ref sellOnHomeDc))
        {
            this.configuration.SellOnHomeDatacenter = sellOnHomeDc;
        }

        this.DrawWorldSelector(
            "Home Datacenter",
            "Home World",
            this.configuration.HomeWorldId,
            worldId => this.configuration.HomeWorldId = worldId);
    }

    private void DrawWorldSelector(
        string datacenterLabel,
        string worldLabel,
        uint currentWorldId,
        Action<uint> setWorldId)
    {
        var currentWorld = this.worldService.GetWorldById(currentWorldId);
        var currentDatacenterId = this.worldService.GetDatacenterIdForWorld(currentWorldId);
        var datacenterIds = this.worldService.GetDatacenterIds();

        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo(
                   datacenterLabel,
                   currentDatacenterId == 0 ? "<Select>" : this.worldService.GetDatacenterName(currentDatacenterId)))
        {
            if (combo)
            {
                foreach (var datacenterId in datacenterIds)
                {
                    var datacenterName = this.worldService.GetDatacenterName(datacenterId);
                    var isSelected = datacenterId == currentDatacenterId;

                    if (ImGui.Selectable(datacenterName, isSelected))
                    {
                        var worlds = this.worldService.GetWorldsForDatacenter(datacenterId);
                        if (worlds.Count > 0)
                            setWorldId(worlds[0].WorldId);
                    }

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
            }
        }

        var worldsForDatacenter = this.worldService.GetWorldsForDatacenter(currentDatacenterId);

        ImGui.SetNextItemWidth(260 * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo(
                   worldLabel,
                   currentWorld?.WorldName ?? "<Select>"))
        {
            if (combo)
            {
                foreach (var world in worldsForDatacenter)
                {
                    var isSelected = world.WorldId == currentWorldId;

                    if (ImGui.Selectable(world.DisplayName, isSelected))
                    {
                        setWorldId(world.WorldId);
                    }

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
            }
        }
    }

    private void DrawScanControls()
    {
        var topN = this.configuration.TopN;
        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Top items", ref topN))
        {
            if (topN < 1)
                topN = 1;

            this.configuration.TopN = topN;
        }

        var minDailySales = this.configuration.MinDailySales;
        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputFloat("Min sales/day", ref minDailySales))
        {
            if (minDailySales < 0)
                minDailySales = 0;

            this.configuration.MinDailySales = minDailySales;
        }

        var historyDays = this.configuration.HistoryDays;
        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("History days", ref historyDays))
        {
            if (historyDays < 1)
                historyDays = 1;

            this.configuration.HistoryDays = historyDays;
        }

        var scanItemLimit = this.configuration.ScanItemLimit;
        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Scan item limit", ref scanItemLimit))
        {
            if (scanItemLimit < 1)
                scanItemLimit = 1;

            this.configuration.ScanItemLimit = scanItemLimit;
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.85f, 0.2f, 1f)))
        {
            ImGui.TextUnformatted("Warning: increasing this number may result in crash!");
        }

        var minAbsoluteProfit = this.configuration.MinAbsoluteProfit;
        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Min profit in gil", ref minAbsoluteProfit))
        {
            if (minAbsoluteProfit < 0)
                minAbsoluteProfit = 0;

            this.configuration.MinAbsoluteProfit = minAbsoluteProfit;
        }

        var minProfitPercent = this.configuration.MinProfitPercent;
        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputFloat("Min profit %", ref minProfitPercent))
        {
            if (minProfitPercent < 0)
                minProfitPercent = 0;

            this.configuration.MinProfitPercent = minProfitPercent;
        }

        var minSalesCount = this.configuration.MinSalesCount;
        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Min sold units", ref minSalesCount))
        {
            if (minSalesCount < 1)
                minSalesCount = 1;

            this.configuration.MinSalesCount = minSalesCount;
        }

        var includeHq = this.configuration.IncludeHq;
        if (ImGui.Checkbox("Include HQ sales", ref includeHq))
        {
            this.configuration.IncludeHq = includeHq;
        }

        if (!this.isScanning)
        {
            if (ImGui.Button("Scan for Flips"))
                this.StartScan();
        }
        else
        {
            if (ImGui.Button("Cancel"))
                this.cts?.Cancel();
        }

        ImGui.SameLine();

        var statusColor = this.status.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
            ? new Vector4(1f, 0.45f, 0.45f, 1f)
            : this.status.StartsWith("Done", StringComparison.OrdinalIgnoreCase)
                ? new Vector4(0.35f, 1f, 0.35f, 1f)
                : Vector4.One;

        using (ImRaii.PushColor(ImGuiCol.Text, statusColor))
        {
            ImGui.TextUnformatted(this.status);
        }

        if (ImGui.Button("Save settings"))
            this.configuration.Save();
    }

    private void DrawResultsTable()
    {
        var tableFlags =
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.Sortable;

        using var table = ImRaii.Table(
            "CrossDcOpportunityTable",
            9,
            tableFlags,
            new Vector2(-1, -1));

        if (!table)
            return;

        ImGui.TableSetupColumn("Item");
        ImGui.TableSetupColumn("Best Buy Scope");
        ImGui.TableSetupColumn("Home Scope");
        ImGui.TableSetupColumn("Buy Price");
        ImGui.TableSetupColumn("Home Avg Sale");
        ImGui.TableSetupColumn("Profit");
        ImGui.TableSetupColumn("Profit %");
        ImGui.TableSetupColumn("Sales/Day");
        ImGui.TableSetupColumn("Route Score");
        ImGui.TableHeadersRow();

        var sortedRows = this.rows;

        var sortSpecs = ImGui.TableGetSortSpecs();
        if (sortSpecs.SpecsCount > 0)
        {
            var spec = sortSpecs.Specs[0];

            sortedRows = spec.ColumnIndex switch
            {
                0 => spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.Name).ToList()
                    : this.rows.OrderByDescending(r => r.Name).ToList(),

                1 => spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.BestBuyScope).ToList()
                    : this.rows.OrderByDescending(r => r.BestBuyScope).ToList(),

                2 => spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.HomeScope).ToList()
                    : this.rows.OrderByDescending(r => r.HomeScope).ToList(),

                3 => spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.BestBuyPrice).ToList()
                    : this.rows.OrderByDescending(r => r.BestBuyPrice).ToList(),

                4 => spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.HomeAverageSalePrice).ToList()
                    : this.rows.OrderByDescending(r => r.HomeAverageSalePrice).ToList(),

                5 => spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.EstimatedProfit).ToList()
                    : this.rows.OrderByDescending(r => r.EstimatedProfit).ToList(),

                6 => spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.ProfitPercent).ToList()
                    : this.rows.OrderByDescending(r => r.ProfitPercent).ToList(),

                7 => spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.SalesPerDay).ToList()
                    : this.rows.OrderByDescending(r => r.SalesPerDay).ToList(),

                8 => spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.RouteScore).ToList()
                    : this.rows.OrderByDescending(r => r.RouteScore).ToList(),

                _ => this.rows
            };
        }

        for (var index = 0; index < sortedRows.Count; index++)
        {
            var row = sortedRows[index];
            var color = GetRowColor(row);
            var prefix = index < 5 ? "★ " : string.Empty;

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(prefix + row.Name);
            }

            ImGui.TableNextColumn();
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(row.BestBuyScope);
            }

            ImGui.TableNextColumn();
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(row.HomeScope);
            }

            ImGui.TableNextColumn();
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(FormatGil(row.BestBuyPrice));
            }

            ImGui.TableNextColumn();
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(FormatGil(row.HomeAverageSalePrice));
            }

            ImGui.TableNextColumn();
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(FormatGil(row.EstimatedProfit));
            }

            ImGui.TableNextColumn();
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted($"{row.ProfitPercent:P1}");
            }

            ImGui.TableNextColumn();
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(row.SalesPerDay.ToString("F2", CultureInfo.CurrentCulture));
            }

            ImGui.TableNextColumn();
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(FormatGil(row.RouteScore));
            }
        }
    }

    private static string FormatGil(double value)
    {
        return value.ToString("N0", CultureInfo.CurrentCulture);
    }

    private static string FormatGil(int value)
    {
        return value.ToString("N0", CultureInfo.CurrentCulture);
    }

    private static Vector4 GetRowColor(CrossDcOpportunityRow row)
    {
        if (row.ProfitPercent >= 0.30 && row.SalesPerDay >= 3)
            return new Vector4(0.35f, 1.00f, 0.35f, 1.00f);

        if (row.ProfitPercent >= 0.15 && row.SalesPerDay >= 1)
            return new Vector4(1.00f, 0.90f, 0.35f, 1.00f);

        if (row.ProfitPercent < 0.08 || row.SalesPerDay < 0.5)
            return new Vector4(1.00f, 0.45f, 0.45f, 1.00f);

        return new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
    }

    private void StartScan()
    {
        this.cts?.Dispose();
        this.cts = new CancellationTokenSource();

        this.isScanning = true;
        this.status = "Starting cross-DC scan...";

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await this.marketScanService.ScanCrossDcAsync(
                    s => this.status = s,
                    this.cts.Token);

                this.rows = result
                    .OrderByDescending(r => r.RouteScore)
                    .ToList();

                this.status = $"Done. {result.Count} cross-DC results.";
            }
            catch (OperationCanceledException)
            {
                this.status = "Cancelled.";
            }
            catch (Exception ex)
            {
                this.status = $"Error: {ex.Message}";
            }
            finally
            {
                this.isScanning = false;
            }
        });
    }

    public void Dispose()
    {
        this.cts?.Cancel();
        this.cts?.Dispose();
    }
}