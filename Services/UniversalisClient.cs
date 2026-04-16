using System.Text.Json;
using TopSellingItems.Models;

namespace TopSellingItems.Services;

public sealed class UniversalisClient : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public UniversalisClient()
    {
        this.httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TopSellingItems/1.0");
    }
    public async Task<List<UniversalisCurrentItem>> GetCurrentDataAsync(
        string scope,
        IEnumerable<uint> itemIds,
        CancellationToken cancellationToken)
    {
        var idArray = itemIds.Distinct().Take(100).ToArray();
        var ids = string.Join(",", idArray);
        var safeScope = Uri.EscapeDataString(scope);

        var url = $"https://universalis.app/api/v2/{safeScope}/{ids}";

        using var response = await this.httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Universalis current-data request failed. Status={(int)response.StatusCode} {response.StatusCode}, " +
                $"scope={scope}, itemCount={idArray.Length}, url={url}, body={body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return ParseCurrentResponse(doc.RootElement, idArray, scope);
    }
    private static List<UniversalisCurrentItem> ParseCurrentResponse(JsonElement root, uint[] requestedIds, string scope)
{
    var items = new List<UniversalisCurrentItem>();

    if (TryGetItemCollection(root, out var collection))
    {
        if (collection.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in collection.EnumerateArray())
            {
                var parsed = ParseCurrentItem(el, scope);
                if (parsed is not null)
                    items.Add(parsed);
            }

            return items;
        }

        if (collection.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in collection.EnumerateObject())
            {
                var parsed = ParseCurrentItem(prop.Value, scope);

                if (parsed is null && uint.TryParse(prop.Name, out var fallbackId))
                {
                    parsed = new UniversalisCurrentItem
                    {
                        ItemId = fallbackId,
                        ScopeName = scope,
                        MinListingPrice =
                            TryGetInt(prop.Value, "minPrice") ??
                            TryGetInt(prop.Value, "minListingPrice") ??
                            ExtractMinListingFromListings(prop.Value) ??
                            0
                    };
                }
                else if (parsed is not null && parsed.ItemId == 0 && uint.TryParse(prop.Name, out var keyedId))
                {
                    parsed.ItemId = keyedId;
                }

                if (parsed is not null)
                    items.Add(parsed);
            }

            return items;
        }
    }

    if (LooksLikeSingleCurrentItem(root))
    {
        var parsed = ParseCurrentItem(root, scope);
        if (parsed is not null)
            items.Add(parsed);

        return items;
    }

    foreach (var id in requestedIds)
    {
        items.Add(new UniversalisCurrentItem
        {
            ItemId = id,
            ScopeName = scope,
            MinListingPrice = 0
        });
    }

    return items;
}

private static UniversalisCurrentItem? ParseCurrentItem(JsonElement el, string scope)
{
    if (!TryGetUInt(el, "itemID", out var itemId) &&
        !TryGetUInt(el, "itemId", out itemId))
    {
        return null;
    }

    return new UniversalisCurrentItem
    {
        ItemId = itemId,
        ScopeName = scope,
        MinListingPrice =
            TryGetInt(el, "minPrice") ??
            TryGetInt(el, "minListingPrice") ??
            ExtractMinListingFromListings(el) ??
            0
    };
}

private static bool LooksLikeSingleCurrentItem(JsonElement root)
{
    return root.ValueKind == JsonValueKind.Object &&
           (root.TryGetProperty("itemID", out _) || root.TryGetProperty("itemId", out _));
}

private static int? ExtractMinListingFromListings(JsonElement el)
{
    if (el.TryGetProperty("listings", out var listings) && listings.ValueKind == JsonValueKind.Array)
    {
        int? min = null;

        foreach (var listing in listings.EnumerateArray())
        {
            var price = TryGetInt(listing, "pricePerUnit");
            if (price is null)
                continue;

            min = min is null ? price : Math.Min(min.Value, price.Value);
        }

        return min;
    }

    return null;
}
    
    public async Task<List<UniversalisHistoryItem>> GetHistoryAsync(
        string scope,
        IEnumerable<uint> itemIds,
        CancellationToken cancellationToken)
    {
        var idArray = itemIds.Distinct().Take(100).ToArray();
        var ids = string.Join(",", idArray);
        var safeScope = Uri.EscapeDataString(scope);

        var url = $"https://universalis.app/api/v2/history/{safeScope}/{ids}";

        using var response = await this.httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Universalis request failed. Status={(int)response.StatusCode} {response.StatusCode}, " +
                $"scope={scope}, itemCount={idArray.Length}, url={url}, body={body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return ParseHistoryResponse(doc.RootElement, idArray);
    }

    private List<UniversalisHistoryItem> ParseHistoryResponse(JsonElement root, uint[] requestedIds)
    {
        var items = new List<UniversalisHistoryItem>();

        if (TryGetItemCollection(root, out var collection))
        {
            // ARRAY FORMAT
            if (collection.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in collection.EnumerateArray())
                {
                    var parsed = ParseItem(el);
                    if (parsed != null)
                        items.Add(parsed);
                }

                return items;
            }

            // OBJECT FORMAT (FIX)
            if (collection.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in collection.EnumerateObject())
                {
                    var parsed = ParseItem(prop.Value);

                    if (parsed == null && uint.TryParse(prop.Name, out var id))
                    {
                        parsed = ParseItemWithFallbackId(prop.Value, id);
                    }
                    else if (parsed != null && parsed.ItemId == 0 && uint.TryParse(prop.Name, out var id2))
                    {
                        parsed.ItemId = id2;
                    }

                    if (parsed != null)
                        items.Add(parsed);
                }

                return items;
            }
        }

        foreach (var id in requestedIds)
            items.Add(new UniversalisHistoryItem { ItemId = id });

        return items;
    }

    private static bool TryGetItemCollection(JsonElement root, out JsonElement collection)
    {
        foreach (var name in new[] { "items", "results", "result" })
        {
            if (root.TryGetProperty(name, out collection))
            {
                if (collection.ValueKind == JsonValueKind.Array ||
                    collection.ValueKind == JsonValueKind.Object)
                    return true;
            }
        }

        collection = default;
        return false;
    }

    private UniversalisHistoryItem? ParseItem(JsonElement el)
    {
        if (!TryGetUInt(el, "itemID", out var itemId) &&
            !TryGetUInt(el, "itemId", out itemId))
        {
            return null;
        }

        var item = new UniversalisHistoryItem
        {
            ItemId = itemId,
            MinListingPrice =
                TryGetInt(el, "minPrice") ??
                TryGetInt(el, "minListingPrice") ??
                0
        };

        if (el.TryGetProperty("entries", out var history) && history.ValueKind == JsonValueKind.Array)
        {
            ParseSales(history, item.RecentHistory);
        }

        return item;
    }

    private UniversalisHistoryItem ParseItemWithFallbackId(JsonElement el, uint id)
    {
        var item = new UniversalisHistoryItem
        {
            ItemId = id,
            MinListingPrice = TryGetInt(el, "minPrice") ?? 0
        };

        if (el.TryGetProperty("entries", out var history) && history.ValueKind == JsonValueKind.Array)
        {
            ParseSales(history, item.RecentHistory);
        }

        return item;
    }

    private static void ParseSales(JsonElement arr, List<UniversalisSale> output)
    {
        foreach (var e in arr.EnumerateArray())
        {
            var sale = new UniversalisSale
            {
                Hq = TryGetBool(e, "hq") ?? false,
                Quantity = TryGetInt(e, "quantity") ?? 1,
                PricePerUnit = TryGetInt(e, "pricePerUnit") ?? 0,
                SoldAt = TryGetLong(e, "timestamp") is long ts
                    ? DateTimeOffset.FromUnixTimeSeconds(ts)
                    : null
            };

            output.Add(sale);
        }
    }

    private static int? TryGetInt(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.TryGetInt32(out var v) ? v : null;

    private static long? TryGetLong(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.TryGetInt64(out var v) ? v : null;

    private static bool? TryGetBool(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True ? true :
           el.TryGetProperty(name, out p) && p.ValueKind == JsonValueKind.False ? false : null;

    private static bool TryGetUInt(JsonElement el, string name, out uint value)
    {
        value = 0;
        if (el.TryGetProperty(name, out var p) && p.TryGetUInt32(out var v))
        {
            value = v;
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        this.httpClient.Dispose();
    }
}