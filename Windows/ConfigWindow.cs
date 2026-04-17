using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
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
        this.Size = new Vector2(560, 520) * ImGuiHelpers.GlobalScale;
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

        this.DrawWorldSelector(
            "Default Datacenter",
            "Default World",
            this.configuration.SelectedWorldId,
            worldId => this.configuration.SelectedWorldId = worldId);

        ImGui.Separator();
        ImGui.TextUnformatted("Default home sell scope");

        var sellOnHomeDc = this.configuration.SellOnHomeDatacenter;
        if (ImGui.Checkbox("Sell on home datacenter by default", ref sellOnHomeDc))
        {
            this.configuration.SellOnHomeDatacenter = sellOnHomeDc;
        }

        this.DrawWorldSelector(
            "Default Home Datacenter",
            "Default Home World",
            this.configuration.HomeWorldId,
            worldId => this.configuration.HomeWorldId = worldId);

        ImGui.Separator();
        ImGui.TextUnformatted("Default scan settings");

        var topN = this.configuration.TopN;
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Default Top N", ref topN))
        {
            if (topN < 1)
                topN = 1;

            this.configuration.TopN = topN;
        }

        var historyDays = this.configuration.HistoryDays;
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("History days", ref historyDays))
        {
            if (historyDays < 1)
                historyDays = 1;

            this.configuration.HistoryDays = historyDays;
        }

        var scanItemLimit = this.configuration.ScanItemLimit;
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Scan item limit", ref scanItemLimit))
        {
            if (scanItemLimit < 1)
                scanItemLimit = 1;

            this.configuration.ScanItemLimit = scanItemLimit;
        }

        var minDailySales = this.configuration.MinDailySales;
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputFloat("Min sales/day", ref minDailySales))
        {
            if (minDailySales < 0)
                minDailySales = 0;

            this.configuration.MinDailySales = minDailySales;
        }

        var minAbsoluteProfit = this.configuration.MinAbsoluteProfit;
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Min profit", ref minAbsoluteProfit))
        {
            if (minAbsoluteProfit < 0)
                minAbsoluteProfit = 0;

            this.configuration.MinAbsoluteProfit = minAbsoluteProfit;
        }

        var minProfitPercent = this.configuration.MinProfitPercent;
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputFloat("Min profit %", ref minProfitPercent))
        {
            if (minProfitPercent < 0)
                minProfitPercent = 0;

            this.configuration.MinProfitPercent = minProfitPercent;
        }

        var minSalesCount = this.configuration.MinSalesCount;
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
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

        ImGui.Separator();

        if (ImGui.Button("Save"))
            this.configuration.Save();
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
}