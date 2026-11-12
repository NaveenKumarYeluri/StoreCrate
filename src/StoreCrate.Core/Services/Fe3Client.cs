using Microsoft.Extensions.Logging;
using StoreCrate.Core.Models;
using StoreLib.Services;

namespace StoreCrate.Core.Services;

/// <summary>
/// Uses StoreLib for the low-level FE3 SOAP calls, then performs the UpdateID → package → URL
/// mapping itself (StoreLib's convenience helper pairs package names and URLs incorrectly).
/// </summary>
public sealed class Fe3Client : IFe3Client
{
    private readonly ILogger<Fe3Client>? _logger;

    public Fe3Client(ILogger<Fe3Client>? logger = null)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<Fe3Package>> GetPackagesAsync(
        string wuCategoryId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wuCategoryId))
        {
            throw new StoreApiException("This product does not expose a delivery category and cannot be downloaded.");
        }

        string xml;
        try
        {
            xml = await FE3Handler.SyncUpdatesAsync(wuCategoryId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new StoreApiException("The Microsoft Store delivery service did not respond. Try again later.", ex);
        }

        var entries = Fe3ResponseParser.Parse(xml);
        if (entries.Count == 0)
        {
            throw new StoreApiException("The delivery service returned no downloadable packages for this product.");
        }

        var packages = new List<Fe3Package>(entries.Count);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IList<Uri> urls;
            try
            {
                urls = await FE3Handler
                    .GetFileUrlsAsync(new[] { entry.UpdateId }, new[] { entry.Revision })
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not resolve a download URL for {Moniker}.", entry.Moniker);
                continue;
            }

            var url = urls.FirstOrDefault();
            if (url is null)
            {
                continue;
            }

            var secureUrl = url.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                ? new UriBuilder(url) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri
                : url;

            packages.Add(new Fe3Package
            {
                UpdateId = entry.UpdateId,
                Revision = entry.Revision,
                Moniker = entry.Moniker,
                PackageType = entry.PackageType,
                Url = secureUrl.ToString(),
            });
        }

        if (packages.Count == 0)
        {
            throw new StoreApiException("No download URLs could be resolved for this product.");
        }

        return packages;
    }
}
