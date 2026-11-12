using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

/// <summary>Enumerates, launches and removes app packages for the current user.</summary>
public interface IInstalledAppService
{
    Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(
        bool includeSystem = false,
        CancellationToken cancellationToken = default);

    Task<InstalledApp?> FindInstalledAsync(
        string packageFamilyName,
        CancellationToken cancellationToken = default);

    Task<InstallResult> UninstallAsync(InstalledApp app, CancellationToken cancellationToken = default);

    Task<bool> LaunchAsync(InstalledApp app, CancellationToken cancellationToken = default);
}
