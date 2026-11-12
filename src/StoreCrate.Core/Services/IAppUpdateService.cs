using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

/// <summary>An available update for an installed app.</summary>
public sealed record AppUpdate(InstalledApp App, StoreProduct Product, Version LatestVersion);

/// <summary>Checks the Store catalog for newer versions of installed apps.</summary>
public interface IAppUpdateService
{
    Task<IReadOnlyList<AppUpdate>> CheckForUpdatesAsync(
        IEnumerable<InstalledApp> apps,
        CancellationToken cancellationToken = default);
}
