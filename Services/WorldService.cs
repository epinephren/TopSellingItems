using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using TopSellingItems.Models;

namespace TopSellingItems.Services;

public sealed class WorldService
{
    private readonly List<WorldOption> worlds;
    private readonly Dictionary<uint, string> datacenterNames;

    public WorldService(IDataManager dataManager)
    {
        var worldSheet = dataManager.GetExcelSheet<World>();
        var worldList = new List<WorldOption>();
        var dcNames = new Dictionary<uint, string>();

        if (worldSheet is null)
        {
            this.worlds = worldList;
            this.datacenterNames = dcNames;
            return;
        }

        foreach (var world in worldSheet)
        {
            if (world.RowId == 0)
                continue;

            var worldName = world.Name.ToString();
            if (string.IsNullOrWhiteSpace(worldName))
                continue;

            var dataCenter = world.DataCenter.ValueNullable;
            if (dataCenter == null)
                continue;

            var datacenterId = dataCenter.Value.RowId;
            if (datacenterId == 0)
                continue;

            var datacenterName = dataCenter.Value.Name.ToString();
            if (string.IsNullOrWhiteSpace(datacenterName))
                continue;

            worldList.Add(new WorldOption
            {
                WorldId = world.RowId,
                DatacenterId = datacenterId,
                WorldName = worldName,
                DatacenterName = datacenterName,
            });

            dcNames[datacenterId] = datacenterName;
        }

        this.worlds = worldList
            .GroupBy(w => w.WorldId)
            .Select(g => g.First())
            .OrderBy(w => w.DatacenterName, StringComparer.Ordinal)
            .ThenBy(w => w.WorldName, StringComparer.Ordinal)
            .ToList();

        this.datacenterNames = dcNames;
    }

    public IReadOnlyList<WorldOption> GetWorlds()
        => this.worlds;

    public IReadOnlyList<uint> GetDatacenterIds()
        => this.datacenterNames.Keys
            .OrderBy(id => this.datacenterNames[id], StringComparer.Ordinal)
            .ToList();

    public string GetDatacenterName(uint datacenterId)
        => this.datacenterNames.TryGetValue(datacenterId, out var name)
            ? name
            : $"Datacenter #{datacenterId}";

    public IReadOnlyList<WorldOption> GetWorldsForDatacenter(uint datacenterId)
    {
        if (datacenterId == 0)
            return this.worlds;

        return this.worlds
            .Where(w => w.DatacenterId == datacenterId)
            .ToList();
    }

    public WorldOption? GetWorldById(uint worldId)
        => this.worlds.FirstOrDefault(w => w.WorldId == worldId);

    public uint GetDatacenterIdForWorld(uint worldId)
        => this.GetWorldById(worldId)?.DatacenterId ?? 0;
}