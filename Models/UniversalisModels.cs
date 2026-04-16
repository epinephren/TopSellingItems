namespace TopSellingItems.Models;

public sealed class UniversalisHistoryItem
{
    public uint ItemId { get; set; }
    public int MinListingPrice { get; set; }
    public List<UniversalisSale> RecentHistory { get; set; } = new();
}

public sealed class UniversalisSale
{
    public bool Hq { get; set; }
    public int Quantity { get; set; }
    public int PricePerUnit { get; set; }
    public DateTimeOffset? SoldAt { get; set; }
}