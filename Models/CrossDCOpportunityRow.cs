namespace TopSellingItems.Models;

public sealed class CrossDcOpportunityRow
{
    public uint ItemId { get; set; }
    public string Name { get; set; } = string.Empty;

    public string HomeScope { get; set; } = string.Empty;
    public string BestBuyScope { get; set; } = string.Empty;

    public double SalesPerDay { get; set; }
    public double HomeAverageSalePrice { get; set; }
    public int HomeMinListingPrice { get; set; }

    public int BestBuyPrice { get; set; }
    public double EstimatedProfit { get; set; }
    public double ProfitPercent { get; set; }
    public double RouteScore { get; set; }
}