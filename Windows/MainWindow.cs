using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;
using System.Globalization;
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

        this.Size = new Vector2(1250, 700);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.RespectCloseHotkey = true;
    }

    public override void Draw()
    {
        this.DrawScanScopeControls();
        ImGui.Separator();
        this.DrawHomeScopeControls();
        ImGui.Separator();
        this.DrawScanControls();
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
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.85f, 0.2f, 1f));
        ImGui.TextUnformatted("(Warning: Scanning ALL Datacenters may take several minutes! Also includes JP/US)");
        ImGui.PopStyleColor();


        var datacenters = this.worldService.GetDatacenters();
        var currentDc = this.configuration.SelectedDatacenter;

        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo("Selected Datacenter", string.IsNullOrWhiteSpace(currentDc) ? "<Select>" : currentDc))
        {
            foreach (var dc in datacenters)
            {
                var isSelected = string.Equals(dc, currentDc, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(dc, isSelected))
                {
                    this.configuration.SelectedDatacenter = dc;

                    var worlds = this.worldService.GetWorldsForDatacenter(dc);
                    if (worlds.Count > 0 &&
                        !worlds.Any(w => string.Equals(w.WorldName, this.configuration.SelectedWorld, StringComparison.OrdinalIgnoreCase)))
                    {
                        this.configuration.SelectedWorld = worlds[0].WorldName;
                    }
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        var worldsForDc = this.worldService.GetWorldsForDatacenter(this.configuration.SelectedDatacenter);

        ImGui.SetNextItemWidth(260);
        if (ImGui.BeginCombo("Selected World", string.IsNullOrWhiteSpace(this.configuration.SelectedWorld) ? "<Select>" : this.configuration.SelectedWorld))
        {
            foreach (var world in worldsForDc)
            {
                var isSelected = string.Equals(world.WorldName, this.configuration.SelectedWorld, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(world.DisplayName, isSelected))
                {
                    this.configuration.SelectedWorld = world.WorldName;
                    this.configuration.SelectedDatacenter = world.DatacenterName;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }

    private void DrawHomeScopeControls()
    {
        ImGui.TextUnformatted("Home sell scope");

        var sellOnHomeDc = this.configuration.SellOnHomeDatacenter;
        if (ImGui.Checkbox("Sell on home datacenter", ref sellOnHomeDc))
        {
            this.configuration.SellOnHomeDatacenter = sellOnHomeDc;
        }

        var datacenters = this.worldService.GetDatacenters();
        var currentHomeDc = this.configuration.HomeDatacenter;
       
        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo("Home Datacenter", string.IsNullOrWhiteSpace(currentHomeDc) ? "<Select>" : currentHomeDc))
        {
            foreach (var dc in datacenters)
            {
                var isSelected = string.Equals(dc, currentHomeDc, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(dc, isSelected))
                {
                    this.configuration.HomeDatacenter = dc;

                    var worlds = this.worldService.GetWorldsForDatacenter(dc);
                    if (worlds.Count > 0 &&
                        !worlds.Any(w => string.Equals(w.WorldName, this.configuration.HomeWorld, StringComparison.OrdinalIgnoreCase)))
                    {
                        this.configuration.HomeWorld = worlds[0].WorldName;
                    }
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        var homeWorldsForDc = this.worldService.GetWorldsForDatacenter(this.configuration.HomeDatacenter);

        ImGui.SetNextItemWidth(260);
        if (ImGui.BeginCombo("Home World", string.IsNullOrWhiteSpace(this.configuration.HomeWorld) ? "<Select>" : this.configuration.HomeWorld))
        {
            foreach (var world in homeWorldsForDc)
            {
                var isSelected = string.Equals(world.WorldName, this.configuration.HomeWorld, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(world.DisplayName, isSelected))
                {
                    this.configuration.HomeWorld = world.WorldName;
                    this.configuration.HomeDatacenter = world.DatacenterName;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }

    private void DrawScanControls()
    {
        var topN = this.configuration.TopN;
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("Top items", ref topN))
        {
            if (topN < 1)
                topN = 1;

            this.configuration.TopN = topN;
        }

        var minDailySales = this.configuration.MinDailySales;
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputFloat("Min sales/day", ref minDailySales))
        {
            if (minDailySales < 0)
                minDailySales = 0;

            this.configuration.MinDailySales = minDailySales;
        }

        var historyDays = this.configuration.HistoryDays;
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("History days", ref historyDays))
        {
            if (historyDays < 1)
                historyDays = 1;

            this.configuration.HistoryDays = historyDays;
        }

        var scanItemLimit = this.configuration.ScanItemLimit;
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("Scan item limit", ref scanItemLimit))
        {
            if (scanItemLimit < 1)
                scanItemLimit = 1;

            this.configuration.ScanItemLimit = scanItemLimit;
        }
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.85f, 0.2f, 1f));
        ImGui.TextUnformatted("Warning: increasing this number may result in crash!");
        ImGui.PopStyleColor();

        var minAbsoluteProfit = this.configuration.MinAbsoluteProfit;
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("Min profit in gil", ref minAbsoluteProfit))
        {
            if (minAbsoluteProfit < 0)
                minAbsoluteProfit = 0;

            this.configuration.MinAbsoluteProfit = minAbsoluteProfit;
        }

        var minProfitPercent = this.configuration.MinProfitPercent;
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputFloat("Min profit %", ref minProfitPercent))
        {
            if (minProfitPercent < 0)
                minProfitPercent = 0;

            this.configuration.MinProfitPercent = minProfitPercent;
        }

        var minSalesCount = this.configuration.MinSalesCount;
        ImGui.SetNextItemWidth(100);
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
        ImGui.TextUnformatted(this.status);

        if (ImGui.Button("Save settings"))
            this.configuration.Save();
    }

    private void DrawResultsTable()
    {
        if (!ImGui.BeginTable(
                "CrossDcOpportunityTable",
                9,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Sortable,
                new Vector2(-1, -1)))
        {
            return;
        }

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
                0 => (spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.Name)
                    : this.rows.OrderByDescending(r => r.Name)).ToList(),

                1 => (spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.BestBuyScope)
                    : this.rows.OrderByDescending(r => r.BestBuyScope)).ToList(),

                2 => (spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.HomeScope)
                    : this.rows.OrderByDescending(r => r.HomeScope)).ToList(),

                3 => (spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.BestBuyPrice)
                    : this.rows.OrderByDescending(r => r.BestBuyPrice)).ToList(),

                4 => (spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.HomeAverageSalePrice)
                    : this.rows.OrderByDescending(r => r.HomeAverageSalePrice)).ToList(),

                5 => (spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.EstimatedProfit)
                    : this.rows.OrderByDescending(r => r.EstimatedProfit)).ToList(),

                6 => (spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.ProfitPercent)
                    : this.rows.OrderByDescending(r => r.ProfitPercent)).ToList(),

                7 => (spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.SalesPerDay)
                    : this.rows.OrderByDescending(r => r.SalesPerDay)).ToList(),

                8 => (spec.SortDirection == ImGuiSortDirection.Ascending
                    ? this.rows.OrderBy(r => r.RouteScore)
                    : this.rows.OrderByDescending(r => r.RouteScore)).ToList(),

                _ => this.rows
            };
        }
        
        

        for (var index = 0; index < sortedRows.Count; index++)
        {
            var row = sortedRows[index];
            var color = GetRowColor(row);

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            var prefix = index < 5 ? "★ " : "";
            ImGui.TextUnformatted(prefix + row.Name);
            ImGui.PopStyleColor();

            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted(row.BestBuyScope);
            ImGui.PopStyleColor();

            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted(row.HomeScope);
            ImGui.PopStyleColor();

            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted(FormatGil(row.BestBuyPrice));
            ImGui.PopStyleColor();

            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted(FormatGil(row.HomeAverageSalePrice));
            ImGui.PopStyleColor();

            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted(FormatGil(row.EstimatedProfit));
            ImGui.PopStyleColor();

            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted($"{row.ProfitPercent:P1}");
            ImGui.PopStyleColor();

            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted(row.SalesPerDay.ToString("F2"));
            ImGui.PopStyleColor();

            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted(FormatGil(row.RouteScore));
            ImGui.PopStyleColor();
        }

        ImGui.EndTable();
    }
    
    private static readonly CultureInfo GilCulture = new("de-DE");

    private static string FormatGil(double value)
    {
        return value.ToString("N0", GilCulture);
    }

    private static string FormatGil(int value)
    {
        return value.ToString("N0", GilCulture);
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