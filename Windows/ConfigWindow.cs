using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;
using TopSellingItems.Services;

namespace TopSellingItems.Windows;

public sealed class ConfigWindow : Window
{
    private readonly Configuration configuration;
    private readonly WorldService worldService;

    public ConfigWindow(Configuration configuration, WorldService worldService)
        : base("Top Selling Items Settings###TopSellingItemsConfig")
    {
        this.configuration = configuration;
        this.worldService = worldService;
        this.Size = new Vector2(520, 420);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var movable = this.configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Config window movable", ref movable))
        {
            this.configuration.IsConfigWindowMovable = movable;
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Default market scope");

        var useDc = this.configuration.UseDatacenterScope;
        if (ImGui.Checkbox("Use datacenter scope by default", ref useDc))
        {
            this.configuration.UseDatacenterScope = useDc;
        }

        var datacenters = this.worldService.GetDatacenters();
        var currentDc = this.configuration.SelectedDatacenter;

        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo("Default Datacenter", string.IsNullOrWhiteSpace(currentDc) ? "<Select>" : currentDc))
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
        if (ImGui.BeginCombo("Default World", string.IsNullOrWhiteSpace(this.configuration.SelectedWorld) ? "<Select>" : this.configuration.SelectedWorld))
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

        ImGui.Separator();
        ImGui.TextUnformatted("Default scan settings");

        var topN = this.configuration.TopN;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Default Top N", ref topN))
        {
            if (topN < 1)
                topN = 1;

            this.configuration.TopN = topN;
        }

        var historyDays = this.configuration.HistoryDays;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("History days", ref historyDays))
        {
            if (historyDays < 1)
                historyDays = 1;

            this.configuration.HistoryDays = historyDays;
        }

        var scanItemLimit = this.configuration.ScanItemLimit;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Scan item limit", ref scanItemLimit))
        {
            if (scanItemLimit < 1)
                scanItemLimit = 1;

            this.configuration.ScanItemLimit = scanItemLimit;
        }

        var minDailySales = this.configuration.MinDailySales;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputFloat("Min sales/day", ref minDailySales))
        {
            if (minDailySales < 0)
                minDailySales = 0;

            this.configuration.MinDailySales = minDailySales;
        }

        var minAbsoluteProfit = this.configuration.MinAbsoluteProfit;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Min profit", ref minAbsoluteProfit))
        {
            if (minAbsoluteProfit < 0)
                minAbsoluteProfit = 0;

            this.configuration.MinAbsoluteProfit = minAbsoluteProfit;
        }

        var minProfitPercent = this.configuration.MinProfitPercent;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputFloat("Min profit %", ref minProfitPercent))
        {
            if (minProfitPercent < 0)
                minProfitPercent = 0;

            this.configuration.MinProfitPercent = minProfitPercent;
        }

        var minSalesCount = this.configuration.MinSalesCount;
        ImGui.SetNextItemWidth(120);
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

        var delayMs = this.configuration.DelayBetweenRequestsMs;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Delay ms", ref delayMs))
        {
            if (delayMs < 0)
                delayMs = 0;

            this.configuration.DelayBetweenRequestsMs = delayMs;
        }

        ImGui.Separator();

        if (ImGui.Button("Save"))
            this.configuration.Save();
    }
}