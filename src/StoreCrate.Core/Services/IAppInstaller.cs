using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

/// <summary>Installs a resolved set of packages on the local machine.</summary>
public interface IAppInstaller
{
    Task<InstallResult> InstallAsync(
        InstallPlan plan,
        IReadOnlyList<DownloadedPackage> packages,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
