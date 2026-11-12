namespace StoreCrate.Core.Services;

/// <summary>Store endpoint templates. These are unofficial, publicly reachable endpoints used by the Microsoft Store client.</summary>
public static class StoreEndpoints
{
    public const string CatalogBase = "https://displaycatalog.mp.microsoft.com/v7.0";
    public const string StorefrontBase = "https://storeedgefd.dsx.mp.microsoft.com/v9.0";
    public const string StorefrontAppVersion = "22110.1401.2.0";
    public const string DeviceFamily = "Windows.Desktop";

    public static Uri Product(string productId, string market, string locale) =>
        new(
            $"{CatalogBase}/products/{Uri.EscapeDataString(productId)}" +
            $"?market={Uri.EscapeDataString(market)}" +
            $"&languages={Uri.EscapeDataString(locale)}" +
            "&fieldsTemplate=Details");

    public static Uri Search(string query, string market, string locale) =>
        new(
            $"{StorefrontBase}/pages/searchResults" +
            $"?appVersion={StorefrontAppVersion}" +
            $"&market={Uri.EscapeDataString(market)}" +
            $"&locale={Uri.EscapeDataString(locale)}" +
            $"&deviceFamily={DeviceFamily}" +
            $"&query={Uri.EscapeDataString(query ?? string.Empty)}");
}
