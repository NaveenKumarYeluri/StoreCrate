using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.App.Services;

/// <summary>
/// Runs an uninstall and watches the package list independently of the WinRT operation.
/// Package removal can take a moment to report completion even after Windows has already
/// removed the package; racing the two keeps the UI from spinning on a stale task.
/// </summary>
public static class UninstallRunner
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    public static async Task<InstallResult> RunAsync(
        IInstalledAppService installedApps,
        InstalledApp app,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(installedApps);
        ArgumentNullException.ThrowIfNull(app);

        var uninstall = installedApps.UninstallAsync(app);
        var removed = WaitUntilRemovedAsync(installedApps, app, timeout ?? DefaultTimeout);

        var first = await Task.WhenAny(uninstall, removed);

        if (first == uninstall)
        {
            return await uninstall;
        }

        if (await removed)
        {
            Observe(uninstall);
            return InstallResult.Success($"Removed {app.DisplayName}.");
        }

        Observe(uninstall);
        return InstallResult.Failed(
            $"Windows did not finish removing {app.DisplayName}. Refresh to check the current state.");
    }

    private static async Task<bool> WaitUntilRemovedAsync(
        IInstalledAppService installedApps,
        InstalledApp app,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval).ConfigureAwait(false);

            try
            {
                if (await installedApps.FindInstalledAsync(app.PackageFamilyName).ConfigureAwait(false) is null)
                {
                    return true;
                }
            }
            catch
            {
                // Keep polling; the package list may be busy while removal is in progress.
            }
        }

        return false;
    }

    private static void Observe(Task task) =>
        _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
}
