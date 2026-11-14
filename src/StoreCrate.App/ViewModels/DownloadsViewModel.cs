using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoreCrate.App.Services;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.App.ViewModels;

public sealed partial class DownloadsViewModel : ViewModelBase
{
    private readonly IInstalledAppService _installedApps;

    public DownloadsViewModel(DownloadManager manager, IInstalledAppService installedApps)
    {
        Manager = manager;
        _installedApps = installedApps;
    }

    public DownloadManager Manager { get; }

    [RelayCommand]
    private async Task OpenAsync(DownloadItem? item) => await LaunchAsync(item);

    [RelayCommand]
    private async Task LaunchAsync(DownloadItem? item)
    {
        if (item is null || !item.IsCompleted)
        {
            return;
        }

        var familyName = item.Plan.Product.PackageFamilyName;
        InstalledApp? app = null;

        if (!string.IsNullOrWhiteSpace(familyName))
        {
            app = await _installedApps.FindInstalledAsync(familyName).ConfigureAwait(true);
        }

        if (app is null && item.Plan.MainPackage is not null)
        {
            app = await _installedApps.FindInstalledAsync(item.Plan.MainPackage.Moniker).ConfigureAwait(true);
        }

        if (app is not null)
        {
            var launched = await _installedApps.LaunchAsync(app).ConfigureAwait(true);
            if (!launched)
            {
                item.DetailText = "Windows could not launch this app.";
            }
        }
        else
        {
            item.DetailText = "App is not registered with Windows.";
        }
    }

    [RelayCommand]
    private void Cancel(DownloadItem? item)
    {
        if (item is not null)
        {
            Manager.Cancel(item);
        }
    }

    [RelayCommand]
    private void Retry(DownloadItem? item)
    {
        if (item is not null)
        {
            Manager.Retry(item);
        }
    }

    [RelayCommand]
    private void ClearCompleted() => Manager.ClearCompleted();

    [RelayCommand]
    private void OpenFolder() => Manager.OpenDownloadsFolder();
}
