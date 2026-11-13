using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

public sealed class AppUpdateService : IAppUpdateService
{
    private const int MaxConcurrency = 4;

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
        // Only Store-signed packages can appear in the catalog; everything else would
        // never match. The package-family lookup resolves the exact product, so there is
        // no risk of picking a similarly named app from search results.
        var candidates = new List<(InstalledApp App, Version Installed)>();
        foreach (var app in apps)
        {
            if (app.IsNonStore)
            {
                continue;
            }

            if (Version.TryParse(app.Version, out var installed))
            {
                candidates.Add((app, installed));
            }
        }

        var updates = new ConcurrentBag<AppUpdate>();

        foreach (var batch in candidates.Chunk(MaxConcurrency))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.WhenAll(batch.Select(item => Task.Run(async () =>
            {
                try
                {
                    var update = await CheckAppAsync(item.App, item.Installed, cancellationToken)
                        .ConfigureAwait(false);
                    if (update is not null)
                    {
                        updates.Add(update);
                    }
                }
                catch (Exception ex) when (ex is StoreApiException or HttpRequestException or TaskCanceledException)
                {
                    _logger?.LogInformation(ex, "Update check failed for {App}.", item.App.DisplayName);
                }
            }, cancellationToken))).ConfigureAwait(false);
        }

        return updates
            .OrderBy(u => u.App.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<AppUpdate?> CheckAppAsync(
        InstalledApp app,
        Version installedVersion,
        CancellationToken cancellationToken)
    {
        var productId = await _catalog
            .GetProductIdByPackageFamilyAsync(app.PackageFamilyName, cancellationToken)
            .ConfigureAwait(false);
        if (productId is null)
        {
            return null;
        }

        var product = await _catalog.GetProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return null;
        }

        var versions = product.Packages
            .Select(p => Version.TryParse(p.Version, out var v) ? v : null)
            .Where(v => v is not null)
            .Select(v => v!)
            .ToList();

        var latest = versions.Count == 0 ? installedVersion : versions.Max() ?? installedVersion;
        if (latest <= installedVersion)
        {
            return null;
        }

        return new AppUpdate(app, product, latest);
    }
}
