using Dalamud.Configuration;
using Dalamud.Plugin;

namespace TopSellingItems;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool IsConfigWindowMovable { get; set; } = true;
    public string SelectedDatacenter { get; set; } = "Light";
    public string SelectedWorld { get; set; } = "Shiva";
    public string HomeDatacenter { get; set; } = "";
    public string HomeWorld { get; set; } = "";
    public bool SellOnHomeDatacenter { get; set; } = true;
    public bool ScanAllDatacenters { get; set; } = true;
    public bool UseDatacenterScope { get; set; } = true;
    public int TopN { get; set; } = 50;
    public float MinDailySales { get; set; } = 1.0f;
    public bool IncludeHq { get; set; } = true;
    public int DelayBetweenRequestsMs { get; set; } = 100;
    public int HistoryDays { get; set; } = 7;
    public int ScanItemLimit { get; set; } = 500;
    public float MinProfitPercent { get; set; } = 0.10f; // 10%
    public int MinAbsoluteProfit { get; set; } = 100;
    public int MinSalesCount { get; set; } = 3;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        this.pluginInterface?.SavePluginConfig(this);
    }
}
