using Dalamud.Configuration;
using Dalamud.Plugin;

namespace TopSellingItems;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool IsConfigWindowMovable { get; set; } = true;

    // Store sheet-backed IDs, not display strings.
    public uint SelectedWorldId { get; set; }
    public uint HomeWorldId { get; set; }

    public bool SellOnHomeDatacenter { get; set; } = true;
    public bool ScanAllDatacenters { get; set; } = true;
    public bool UseDatacenterScope { get; set; } = true;

    public int TopN { get; set; } = 50;
    public float MinDailySales { get; set; } = 1.0f;
    public bool IncludeHq { get; set; } = true;
    public int HistoryDays { get; set; } = 7;
    public int ScanItemLimit { get; set; } = 500;
    public float MinProfitPercent { get; set; } = 0.10f;
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