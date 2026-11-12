using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

/// <summary>Loads and persists <see cref="AppSettings"/>.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    event EventHandler? Changed;

    string DownloadDirectory { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}
