using Microsoft.Extensions.Logging;
using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

public sealed class AppUpdateService : IAppUpdateService
{
    private readonly IStoreCatalogClient _catalog;
    private readonly ILogger<AppUpdateService>? _logger;

    public AppUpdateService(IStoreCatalogClient catalog, ILogger<AppUpdateService>? logger = null)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AppUpdate>> CheckForUpdatesAsync(
        IEnumerable<InstalledApp> apps,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<AppUpdate>();

        foreach (var app in apps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Version.TryParse(app.Version, out var installedVersion))
            {
                continue;
            }

            try
            {
                var results = await _catalog.SearchAsync(app.DisplayName, cancellationToken).ConfigureAwait(false);
                var match = results.FirstOrDefault(r =>
                    r.PackageFamilyNames.Any(pfn =>
                        string.Equals(pfn, app.PackageFamilyName, StringComparison.OrdinalIgnoreCase)));

                if (match is null)
                {
                    continue;
                }

                var product = await _catalog.GetProductAsync(match.ProductId, cancellationToken).ConfigureAwait(false);
                if (product is null)
                {
                    continue;
                }

                var latest = product.Packages
                    .Select(p => Version.TryParse(p.Version, out var v) ? v : null)
                    .Where(v => v is not null)
                    .Select(v => v!)
                    .DefaultIfEmpty(installedVersion)
                    .Max();

                if (latest > installedVersion)
                {
                    updates.Add(new AppUpdate(app, product, latest));
                }
            }
            catch (Exception ex) when (ex is StoreApiException or HttpRequestException or TaskCanceledException)
            {
                _logger?.LogInformation(ex, "Update check failed for {App}.", app.DisplayName);
            }
        }

        return updates;
    }
}
