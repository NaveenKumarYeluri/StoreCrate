using System.Net;
using Microsoft.Extensions.Logging;
using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

public sealed class StoreCatalogClient : IStoreCatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settings;
    private readonly ILogger<StoreCatalogClient>? _logger;

    public StoreCatalogClient(
        HttpClient httpClient,
        ISettingsService settings,
        ILogger<StoreCatalogClient>? logger = null)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StoreSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;
        var uri = StoreEndpoints.Search(query, settings.Market, settings.Locale);
        var json = await GetStringAsync(uri, allowNotFound: false, cancellationToken).ConfigureAwait(false);
        return json is null ? Array.Empty<StoreSearchResult>() : StoreJsonParser.ParseSearch(json);
    }

    public async Task<StoreProduct?> GetProductAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return null;
        }

        var settings = _settings.Current;
        var uri = StoreEndpoints.Product(productId.Trim(), settings.Market, settings.Locale);
        var json = await GetStringAsync(uri, allowNotFound: true, cancellationToken).ConfigureAwait(false);
        return json is null ? null : StoreJsonParser.ParseProduct(json);
    }

    public async Task<string?> GetProductIdByPackageFamilyAsync(
        string packageFamilyName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageFamilyName))
        {
            return null;
        }

        var settings = _settings.Current;
        var uri = StoreEndpoints.LookupByPackageFamilyName(
            packageFamilyName.Trim(),
            settings.Market,
            settings.Locale);
        var json = await GetStringAsync(uri, allowNotFound: true, cancellationToken).ConfigureAwait(false);
        return json is null ? null : StoreJsonParser.ParseLookupProductId(json);
    }

    private async Task<string?> GetStringAsync(Uri uri, bool allowNotFound, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd($"StoreCrate/{AppInfo.VersionString}");
                request.Headers.Accept.ParseAdd("application/json");

                using var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound && allowNotFound)
                {
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                {
                    _logger?.LogWarning("Store request to {Uri} returned {Status} (attempt {Attempt}).", uri, response.StatusCode, attempt);
                    if (attempt == maxAttempts)
                    {
                        response.EnsureSuccessStatusCode();
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt * attempt), cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                _logger?.LogWarning(ex, "Store request to {Uri} failed (attempt {Attempt}).", uri, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt * attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new StoreApiException($"Unable to reach the Microsoft Store catalog at {uri}.");
    }
}
