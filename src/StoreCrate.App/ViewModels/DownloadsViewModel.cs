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

        foreach (var item in manager.Items)
        {
            AttachItemHandlers(item);
        }
    }

    public DownloadManager Manager { get; }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenAsync(DownloadItem? item) => await LaunchAsync(item);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LaunchAsync(DownloadItem? item)
    {
        if (item is null)
        {
            return;
        }

        AttachItemHandlers(item);
        await item.OpenCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void Cancel(DownloadItem? item)
    {
        if (item is not null)
        {
            item.CancelCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void Retry(DownloadItem? item)
    {
        if (item is not null)
        {
            item.RetryCommand.Execute(null);
        }
    }

    private void AttachItemHandlers(DownloadItem item)
    {
        item.OpenHandler ??= Manager.LaunchAsync;
        item.CancelHandler ??= Manager.Cancel;
        item.RetryHandler ??= Manager.Retry;
    }

    [RelayCommand]
    private void ClearCompleted() => Manager.ClearCompleted();

    [RelayCommand]
    private void OpenFolder() => Manager.OpenDownloadsFolder();
}
