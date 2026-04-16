using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using TopSellingItems.Models;

namespace TopSellingItems.Services;

public sealed class WorldService
{
    private readonly List<WorldOption> worlds;
    private readonly List<string> datacenters;

    public WorldService(IDataManager dataManager)
    {
        var worldSheet = dataManager.GetExcelSheet<World>();
        var worldList = new List<WorldOption>();
        var dcSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (worldSheet is null)
        {
            this.worlds = worldList;
            this.datacenters = new List<string>();
            return;
        }

        foreach (var world in worldSheet)
        {
            if (world.RowId == 0)
                continue;

            var worldName = world.Name.ToString();
            if (string.IsNullOrWhiteSpace(worldName))
                continue;

            string datacenterName;
            try
            {
                datacenterName = world.DataCenter.Value.Name.ToString();
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(datacenterName))
                continue;

            worldList.Add(new WorldOption
            {
                WorldName = worldName,
                DatacenterName = datacenterName,
            });
            dcSet.Add(datacenterName);
        }

        this.worlds = worldList
            .GroupBy(w => (w.WorldName, w.DatacenterName))
            .Select(g => g.First())
            .OrderBy(w => w.DatacenterName, StringComparer.Ordinal)
            .ThenBy(w => w.WorldName, StringComparer.Ordinal)
            .ToList();

        this.datacenters = dcSet
            .OrderBy(dc => dc, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<WorldOption> GetWorlds() => this.worlds;

    public IReadOnlyList<string> GetDatacenters() => this.datacenters;

    public IReadOnlyList<WorldOption> GetWorldsForDatacenter(string? datacenter)
    {
        if (string.IsNullOrWhiteSpace(datacenter))
            return this.worlds;

        return this.worlds
            .Where(w => string.Equals(w.DatacenterName, datacenter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public WorldOption? GetWorldByName(string? worldName)
    {
        if (string.IsNullOrWhiteSpace(worldName))
            return null;

        return this.worlds.FirstOrDefault(w => string.Equals(w.WorldName, worldName, StringComparison.OrdinalIgnoreCase));
    }
}
