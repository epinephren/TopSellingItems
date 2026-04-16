namespace TopSellingItems.Models;

public sealed class UniversalisCurrentItem
{
    public uint ItemId { get; set; }
    public int MinListingPrice { get; set; }
    public string ScopeName { get; set; } = string.Empty;
}