using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

/// <summary>Downloads packages with progress reporting and HTTP range resume.</summary>
public interface IDownloadService
{
    Task<DownloadedPackage> DownloadAsync(
        Fe3Package package,
        string destinationDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
