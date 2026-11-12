using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace StoreCrate.App.Services;

/// <summary>Small UI helpers that need access to the main window.</summary>
public sealed class DialogService
{
    public async Task<string?> PickFolderAsync(string? startDirectory = null)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is not { } window)
        {
            return null;
        }

        var options = new FolderPickerOpenOptions
        {
            Title = "Choose a download folder",
            AllowMultiple = false,
        };

        if (!string.IsNullOrWhiteSpace(startDirectory) && Directory.Exists(startDirectory))
        {
            try
            {
                options.SuggestedStartLocation = await window.StorageProvider
                    .TryGetFolderFromPathAsync(startDirectory)
                    .ConfigureAwait(true);
            }
            catch
            {
                // The picker opens at its default location instead.
            }
        }

        var folders = await window.StorageProvider.OpenFolderPickerAsync(options).ConfigureAwait(true);
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    public void OpenInExplorer(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                {
                    UseShellExecute = true,
                });
            }
        }
        catch
        {
            // Opening a folder is best effort.
        }
    }

    public void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // Opening a link is best effort.
        }
    }
}
