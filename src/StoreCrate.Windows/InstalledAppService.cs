using Microsoft.Extensions.Logging;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace StoreCrate.Windows;

public sealed class InstalledAppService : IInstalledAppService
{
    private readonly ILogger<InstalledAppService>? _logger;

    public InstalledAppService(ILogger<InstalledAppService>? logger = null)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(
        bool includeNonStore = false,
        CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<InstalledApp>>(() => Enumerate(includeNonStore), cancellationToken);

    public Task<InstalledApp?> FindInstalledAsync(
        string packageFamilyName,
        CancellationToken cancellationToken = default) =>
        Task.Run<InstalledApp?>(
            () =>
            {
                if (string.IsNullOrWhiteSpace(packageFamilyName))
                {
                    return null;
                }

                try
                {
                    var manager = new PackageManager();
                    // Try exact-match first: use FindPackageForUser with the full PFN
                    // returned by the catalog. If that fails (different format), fall back
                    // to FindPackagesForUser which is more lenient and may find the package
                    // even when the PFN format differs between catalog and installed state.
                    var package = string.IsNullOrWhiteSpace(packageFamilyName)
                        ? null
                        : manager.FindPackageForUser(string.Empty, packageFamilyName)
                        ?? manager
                           .FindPackagesForUser(string.Empty, packageFamilyName)
                           .FirstOrDefault();

                    return package is null ? null : ToInstalledApp(package);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Could not find installed package {FamilyName}.", packageFamilyName);
                    return null;
                }
            },
            cancellationToken);

    public async Task<InstallResult> UninstallAsync(
        InstalledApp app,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        try
        {
            // Run the WinRT call on a worker (MTA) thread. Calling RemovePackageAsync from
            // the UI thread leaves the async operation unretrievable in some cases, which
            // looks like an uninstall that never finishes.
            var result = await Task.Run(async () =>
            {
                var manager = new PackageManager();
                var operation = manager.RemovePackageAsync(app.PackageFullName);
                using var registration = cancellationToken.Register(() =>
                {
                    try
                    {
                        operation.Cancel();
                    }
                    catch
                    {
                        // Best effort.
                    }
                });

                return await operation;
            }, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (result.ExtendedErrorCode is null)
            {
                return InstallResult.Success($"Removed {app.DisplayName}.");
            }

            var code = $"0x{result.ExtendedErrorCode.HResult:X8}";
            return InstallResult.Failed(
                string.IsNullOrWhiteSpace(result.ErrorText) ? $"Uninstall failed ({code})." : result.ErrorText,
                code);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Uninstall failed for {App}.", app.DisplayName);
            return InstallResult.Failed($"Uninstall failed: {ex.Message}");
        }
    }

    public async Task<bool> LaunchAsync(InstalledApp app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        try
        {
            // Launch on a worker (MTA) thread for the same reason as uninstall.
            return await Task.Run(async () =>
            {
                var manager = new PackageManager();
                var package = manager.FindPackageForUser(string.Empty, app.PackageFullName);
                if (package is null)
                {
                    return false;
                }

                var entries = await package.GetAppListEntriesAsync();
                var entry = entries.FirstOrDefault();
                return entry is not null && await entry.LaunchAsync();
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Could not launch {App}.", app.DisplayName);
            return false;
        }
    }

    private IReadOnlyList<InstalledApp> Enumerate(bool includeNonStore)
    {
        var manager = new PackageManager();
        var apps = new List<InstalledApp>();

        foreach (var package in manager.FindPackagesForUser(string.Empty))
        {
            try
            {
                if (package.IsFramework || package.IsResourcePackage || package.IsBundle)
                {
                    continue;
                }

                // Anything not Store-signed (inbox components, enterprise or sideloaded
                // packages) is hidden unless explicitly requested; these are also the
                // packages the Microsoft Store catalog does not list.
                var isNonStore = package.SignatureKind != PackageSignatureKind.Store;
                if (isNonStore && !includeNonStore)
                {
                    continue;
                }

                apps.Add(ToInstalledApp(package));
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Skipped a package that could not be inspected.");
            }
        }

        return apps
            .OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static InstalledApp ToInstalledApp(Package package)
    {
        var version = package.Id.Version;
        var displayName = Safe(() => package.DisplayName) ?? package.Id.Name;

        return new InstalledApp
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? package.Id.Name : displayName,
            PackageFullName = package.Id.FullName,
            PackageFamilyName = package.Id.FamilyName,
            Version = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}",
            Publisher = Safe(() => package.PublisherDisplayName),
            InstallLocation = Safe(() => package.InstalledLocation?.Path),
            IsNonStore = Safe(() => package.SignatureKind != PackageSignatureKind.Store),
        };
    }

    private static string? Safe(Func<string?> accessor)
    {
        try
        {
            return accessor();
        }
        catch
        {
            return null;
        }
    }

    private static bool Safe(Func<bool> accessor)
    {
        try
        {
            return accessor();
        }
        catch
        {
            return false;
        }
    }
}
