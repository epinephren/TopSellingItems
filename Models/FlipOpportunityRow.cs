namespace TopSellingItems.Models;

public sealed class FlipOpportunityRow
{
    public uint ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double SalesPerDay { get; set; }
    public double AverageSalePrice { get; set; }
    public int MinListingPrice { get; set; }
    public double EstimatedProfit { get; set; }
    public double ProfitPercent { get; set; }
    public double FlipScore { get; set; }
}