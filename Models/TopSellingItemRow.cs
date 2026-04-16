namespace TopSellingItems.Models;

public sealed class TopSellingItemRow
{
    public uint ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double DailySales { get; set; }
    public double AverageSalePrice { get; set; }
    public int MinListingPrice { get; set; }
}
