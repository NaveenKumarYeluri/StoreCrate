using System.Globalization;
using System.Text.Json;
using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

/// <summary>
/// Parses the JSON payloads returned by the Display Catalog and Storefront search endpoints.
/// Kept free of HTTP so it can be unit tested with captured fixtures.
/// </summary>
public static class StoreJsonParser
{
    public static StoreProduct? ParseProduct(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Object &&
               document.RootElement.TryGetProperty("Product", out var product)
            ? ParseProductElement(product)
            : null;
    }

    public static StoreProduct? ParseProductElement(JsonElement product)
    {
        if (product.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var productId = Str(product, "ProductId");
        if (string.IsNullOrEmpty(productId))
        {
            return null;
        }

        var localized = Items(product, "LocalizedProperties").FirstOrDefault();
        var title = Str(localized, "ProductTitle")
                    ?? Str(localized, "ShortTitle")
                    ?? Str(localized, "FriendlyTitle")
                    ?? productId;

        var images = new List<StoreImage>();
        foreach (var image in Items(localized, "Images"))
        {
            var url = StoreImage.NormalizeUrl(Str(image, "Uri"));
            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            images.Add(new StoreImage(
                Str(image, "ImagePurpose") ?? "Unknown",
                url,
                Int(image, "Width") ?? 0,
                Int(image, "Height") ?? 0));
        }

        var market = Items(product, "MarketProperties").FirstOrDefault();
        var usage = Items(market, "UsageData").ToList();
        var allTime = usage.FirstOrDefault(u =>
            string.Equals(Str(u, "AggregateTimeSpan"), "AllTime", StringComparison.OrdinalIgnoreCase));
        if (allTime.ValueKind == JsonValueKind.Undefined)
        {
            allTime = usage.FirstOrDefault();
        }

        var properties = Obj(product, "Properties") ?? default;
        var categories = Items(properties, "Categories")
            .Select(c => c.ValueKind == JsonValueKind.String ? c.GetString() : Str(c, "Name"))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .ToList();
        if (categories.Count == 0)
        {
            var category = Str(properties, "Category");
            if (!string.IsNullOrWhiteSpace(category))
            {
                categories.Add(category);
            }
        }

        var skuWrapper = Items(product, "DisplaySkuAvailabilities").FirstOrDefault();
        var sku = Obj(skuWrapper, "Sku") ?? default;
        var availability = Items(skuWrapper, "Availabilities").FirstOrDefault();
        var skuProperties = Obj(sku, "Properties") ?? default;
        var fulfillment = Obj(skuProperties, "FulfillmentData") ?? default;
        var skuMarket = Items(sku, "MarketProperties").FirstOrDefault();

        var packages = new List<CatalogPackage>();
        foreach (var package in Items(skuProperties, "Packages"))
        {
            var fullName = Str(package, "PackageFullName");
            var version = fullName is not null
                ? PackageMoniker.Parse(fullName).VersionText
                : Str(package, "Version");
            if (!Version.TryParse(version, out _))
            {
                version = null;
            }

            packages.Add(new CatalogPackage
            {
                Architectures = Items(package, "Architectures")
                    .Where(a => a.ValueKind == JsonValueKind.String)
                    .Select(a => a.GetString()!)
                    .ToList(),
                Version = version,
                MaxDownloadSizeBytes = Long(package, "MaxDownloadSizeInBytes"),
                MaxInstallSizeBytes = Long(package, "MaxInstallSizeInBytes"),
                PackageFormat = Str(package, "PackageFormat"),
                PackageFullName = fullName,
                PackageFamilyName = Str(package, "PackageFamilyName"),
            });
        }

        return new StoreProduct
        {
            ProductId = productId,
            Title = title,
            Publisher = Str(localized, "PublisherName"),
            Developer = Str(localized, "DeveloperName"),
            ShortDescription = NullIfEmpty(Str(localized, "ShortDescription")),
            Description = NullIfEmpty(Str(localized, "ProductDescription")) ?? NullIfEmpty(Str(localized, "ShortDescription")),
            PackageFamilyName = Str(properties, "PackageFamilyName"),
            PackageIdentityName = Str(properties, "PackageIdentityName"),
            PublisherId = Str(properties, "PublisherId"),
            WuCategoryId = Str(fulfillment, "WuCategoryId"),
            Market = Str(product, "Market") ?? Str(market, "Market"),
            Locale = Str(localized, "Language"),
            AverageRating = Dbl(allTime, "AverageRating"),
            RatingCount = Int(allTime, "RatingCount"),
            Price = ParsePrice(availability) ?? ParsePrice(skuMarket) ?? ParsePrice(market),
            ReleaseDate = Date(market, "OriginalReleaseDate") ?? Date(properties, "OriginalReleaseDate"),
            Categories = categories,
            Images = images,
            Packages = packages,
        };
    }

    public static IReadOnlyList<StoreSearchResult> ParseSearch(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!TryFindArray(document.RootElement, "SearchResults", 0, out var searchResults))
        {
            return Array.Empty<StoreSearchResult>();
        }

        var results = new List<StoreSearchResult>();
        foreach (var item in searchResults.EnumerateArray())
        {
            var productId = Str(item, "ProductId");
            var title = Str(item, "Title");
            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(title))
            {
                continue;
            }

            string? logo = null;
            string? tile = null;
            string? background = null;
            foreach (var image in Items(item, "Images"))
            {
                var type = Str(image, "ImageType") ?? Str(image, "ImagePurpose") ?? string.Empty;
                var url = StoreImage.NormalizeUrl(Str(image, "Url") ?? Str(image, "Uri"));
                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                if (type.Contains("logo", StringComparison.OrdinalIgnoreCase) && logo is null)
                {
                    logo = url;
                    background ??= Str(image, "BackgroundColor");
                }
                else if (type.Contains("tile", StringComparison.OrdinalIgnoreCase) && tile is null)
                {
                    tile = url;
                }
            }

            results.Add(new StoreSearchResult
            {
                ProductId = productId,
                Title = title,
                Publisher = Str(item, "PublisherName"),
                Description = Str(item, "LongDescription") ?? Str(item, "Description"),
                AverageRating = Dbl(item, "AverageRating"),
                RatingCount = Int(item, "RatingsCount"),
                Price = Str(item, "DisplayPrice"),
                LogoUrl = logo ?? tile,
                TileUrl = tile ?? logo,
                BackgroundColor = background,
                Categories = Items(item, "Categories")
                    .Where(c => c.ValueKind == JsonValueKind.String)
                    .Select(c => c.GetString()!)
                    .ToList(),
                PackageFamilyNames = Items(item, "PackageFamilyNames")
                    .Where(p => p.ValueKind == JsonValueKind.String)
                    .Select(p => p.GetString()!)
                    .ToList(),
                ReleaseDate = Date(item, "ReleaseDate"),
            });
        }

        return results;
    }

    private static string? ParsePrice(JsonElement market)
    {        var orderManagement = Obj(market, "OrderManagementData");
        if (orderManagement is not { } order)
        {
            return null;
        }

        var price = Obj(order, "Price");
        if (price is not { } priceElement)
        {
            return null;
        }

        var listPrice = Dbl(priceElement, "ListPrice");
        var currency = Str(priceElement, "CurrencyCode") ?? "USD";
        if (listPrice is null or 0)
        {
            return "Free";
        }

        return $"{listPrice.Value.ToString("0.00", CultureInfo.InvariantCulture)} {currency}";
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Str(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static bool TryFindArray(JsonElement element, string name, int depth, out JsonElement found)
    {
        found = default;
        if (depth > 12)
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(name) && property.Value.ValueKind == JsonValueKind.Array)
                {
                    found = property.Value;
                    return true;
                }

                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array &&
                    TryFindArray(property.Value, name, depth + 1, out found))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array &&
                    TryFindArray(item, name, depth + 1, out found))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static JsonElement? Obj(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    private static JsonElement.ArrayEnumerator Items(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray();
        }

        return default;
    }

    private static int? Int(JsonElement element, string name)
    {
        var value = Value(element, name);
        if (value is null)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.Value.ValueKind == JsonValueKind.String &&
               int.TryParse(value.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long? Long(JsonElement element, string name)
    {
        var value = Value(element, name);
        if (value is null)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt64(out var number))
        {
            return number;
        }

        return value.Value.ValueKind == JsonValueKind.String &&
               long.TryParse(value.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static double? Dbl(JsonElement element, string name)
    {
        var value = Value(element, name);
        if (value is null)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out var number))
        {
            return number;
        }

        return value.Value.ValueKind == JsonValueKind.String &&
               double.TryParse(value.Value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? Date(JsonElement element, string name) =>
        DateTimeOffset.TryParse(
            Str(element, name),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;

    private static JsonElement? Value(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value
            : null;
}
