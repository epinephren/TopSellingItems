namespace TopSellingItems.Models;

public sealed class WorldOption
{
    public uint WorldId { get; init; }
    public uint DatacenterId { get; init; }

    public string WorldName { get; init; } = string.Empty;
    public string DatacenterName { get; init; } = string.Empty;

    public string DisplayName => $"{WorldName} ({DatacenterName})";
}