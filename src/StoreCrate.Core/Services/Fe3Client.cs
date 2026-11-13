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
    private readonly IFe3Transport _transport;
    private readonly ILogger<Fe3Client>? _logger;

    public Fe3Client(ILogger<Fe3Client>? logger = null)
        : this(new StoreLibFe3Transport(), logger)
    {
    }

    internal Fe3Client(IFe3Transport transport, ILogger<Fe3Client>? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
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
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            xml = await _transport.SyncUpdatesAsync(wuCategoryId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "FE3 SyncUpdates for {Category} failed after {Elapsed} ms.", wuCategoryId, stopwatch.ElapsedMilliseconds);
            throw new StoreApiException("The Microsoft Store delivery service did not respond. Try again later.", ex);
        }

        _logger?.LogInformation("FE3 SyncUpdates for {Category} returned {Length} bytes in {Elapsed} ms.", wuCategoryId, xml.Length, stopwatch.ElapsedMilliseconds);

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
                urls = await _transport
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

            // FE3 hands out plain-HTTP delivery URLs and Microsoft's own client downloads them as-is.
            // Upgrading to HTTPS breaks on the bare tlu.dl.delivery.mp.microsoft.com host: its
            // certificates only cover *.sb.tlu… / *.sf.tlu…, so the CDN serves a fallback certificate
            // and the handshake fails with SEC_E_WRONG_PRINCIPAL. The payload is a signed package,
            // which Windows validates during install, so keep the scheme FE3 gave us.
            packages.Add(new Fe3Package
            {
                UpdateId = entry.UpdateId,
                Revision = entry.Revision,
                Moniker = entry.Moniker,
                PackageType = entry.PackageType,
                Url = url.ToString(),
            });
        }

        if (packages.Count == 0)
        {
            throw new StoreApiException("No download URLs could be resolved for this product.");
        }

        return packages;
    }
}

/// <summary>Thin seam over StoreLib's static FE3 helpers so the client can be tested.</summary>
internal interface IFe3Transport
{
    Task<string> SyncUpdatesAsync(string wuCategoryId);

    Task<IList<Uri>> GetFileUrlsAsync(string[] updateIds, string[] revisions);
}

internal sealed class StoreLibFe3Transport : IFe3Transport
{
    public Task<string> SyncUpdatesAsync(string wuCategoryId) =>
        FE3Handler.SyncUpdatesAsync(wuCategoryId);

    public Task<IList<Uri>> GetFileUrlsAsync(string[] updateIds, string[] revisions) =>
        FE3Handler.GetFileUrlsAsync(updateIds, revisions);
}
