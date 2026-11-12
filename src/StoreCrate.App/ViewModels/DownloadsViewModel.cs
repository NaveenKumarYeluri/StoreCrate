using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoreCrate.App.Services;

namespace StoreCrate.App.ViewModels;

public sealed partial class DownloadsViewModel : ViewModelBase
{
    public DownloadsViewModel(DownloadManager manager)
    {
        Manager = manager;
    }

    public DownloadManager Manager { get; }

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
