using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace TopSellingItems.Services;

public sealed class ItemScanner
{
    private readonly IDataManager dataManager;
    private Dictionary<uint, string>? cachedNames;

    public ItemScanner(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    public IReadOnlyList<uint> GetCandidateItemIds()
    {
        var itemSheet = this.dataManager.GetExcelSheet<Item>();
        var result = new List<uint>(5000);

        foreach (var item in itemSheet)
        {
            if (item.RowId == 0)
                continue;

            if (item.Name.IsEmpty)
                continue;

            if (item.StackSize == 0)
                continue;

            result.Add(item.RowId);
        }

        return result;
    }

    public string GetItemName(uint itemId)
    {
        this.cachedNames ??= this.BuildNameCache();
        return this.cachedNames.TryGetValue(itemId, out var name) ? name : $"Item #{itemId}";
    }

    private Dictionary<uint, string> BuildNameCache()
    {
        var itemSheet = this.dataManager.GetExcelSheet<Item>();
        var dict = new Dictionary<uint, string>();

        foreach (var item in itemSheet)
        {
            if (item.RowId == 0 || item.Name.IsEmpty)
                continue;

            dict[item.RowId] = item.Name.ToString();
        }

        return dict;
    }
}
