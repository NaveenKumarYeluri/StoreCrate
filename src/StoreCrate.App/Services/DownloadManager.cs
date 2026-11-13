using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using StoreCrate.App.ViewModels;
using StoreCrate.Core.Logging;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.App.Services;

/// <summary>Runs the download → install pipeline for queued products.</summary>
public sealed partial class DownloadManager : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;
    private readonly SemaphoreSlim _gate;
    private readonly Dictionary<DownloadItem, CancellationTokenSource> _cancellations = new();

    public DownloadManager(IServiceProvider services, ISettingsService settings)
    {
        _services = services;
        _settings = settings;
        _gate = new SemaphoreSlim(
            Math.Clamp(settings.Current.MaxConcurrentDownloads, 1, 4),
            Math.Clamp(settings.Current.MaxConcurrentDownloads, 1, 4));
    }

    public ObservableCollection<DownloadItem> Items { get; } = new();

    public event EventHandler<StoreProduct>? InstallCompleted;

    [ObservableProperty]
    private int _activeCount;

    public bool HasItems => Items.Count > 0;

    public bool HasActive => ActiveCount > 0;

    public bool HasCompleted => Items.Any(i => i.IsCompleted);

    public DownloadItem Enqueue(InstallPlan plan)
    {
        var running = Items.FirstOrDefault(i =>
            i.IsActive && string.Equals(i.ProductId, plan.Product.ProductId, StringComparison.OrdinalIgnoreCase));

        if (running is not null)
        {
            return running;
        }

        var item = new DownloadItem(plan);
        Items.Insert(0, item);
        OnPropertyChanged(nameof(HasItems));
        _ = ProcessAsync(item);
        return item;
    }

    public void Cancel(DownloadItem item)
    {
        if (_cancellations.TryGetValue(item, out var cancellation))
        {
            cancellation.Cancel();
        }
    }

    public void Retry(DownloadItem item)
    {
        if (item.IsActive)
        {
            return;
        }

        item.Progress = 0;
        item.ErrorMessage = null;
        item.State = DownloadState.Queued;
        item.StatusText = "Queued";
        item.DetailText = "Waiting for a download slot";
        _ = ProcessAsync(item);
    }

    public void ClearCompleted()
    {
        foreach (var item in Items.Where(i => !i.IsActive).ToList())
        {
            Items.Remove(item);
        }

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasCompleted));
    }

    public void OpenDownloadsFolder() =>
        _services.GetRequiredService<DialogService>().OpenInExplorer(_settings.DownloadDirectory);

    private async Task ProcessAsync(DownloadItem item)
    {
        var cancellation = new CancellationTokenSource();
        _cancellations[item] = cancellation;
        Recalculate();

        try
        {
            await _gate.WaitAsync(cancellation.Token).ConfigureAwait(true);

            try
            {
                item.State = DownloadState.Downloading;
                item.StatusText = "Downloading";
                item.DetailText = "Preparing download";

                var directory = Path.Combine(_settings.DownloadDirectory, item.ProductId);
                Directory.CreateDirectory(directory);

                var downloadService = _services.GetRequiredService<IDownloadService>();
                var packages = item.Plan.AllPackages;
                var downloaded = new List<DownloadedPackage>(packages.Count);

                for (var index = 0; index < packages.Count; index++)
                {
                    cancellation.Token.ThrowIfCancellationRequested();

                    var package = packages[index];
                    var slot = index;
                    item.DetailText = $"Package {index + 1} of {packages.Count} · {package.Name}";

                    var progress = new DispatcherProgress<DownloadProgress>(value =>
                    {
                        var fraction = (slot + value.Fraction) / packages.Count;
                        item.Progress = Math.Clamp(fraction * 100, 0, 100);
                        item.DetailText = $"{value.FileName} · {value.SizeText}";
                    });

                    downloaded.Add(await downloadService
                        .DownloadAsync(package, directory, progress, cancellation.Token)
                        .ConfigureAwait(true));
                }

                item.Progress = 100;
                item.State = DownloadState.Installing;
                item.StatusText = "Installing";
                item.DetailText = "Registering packages with Windows";

                var installer = _services.GetRequiredService<IAppInstaller>();
                var installProgress = new DispatcherProgress<double>(fraction =>
                {
                    item.Progress = Math.Clamp(fraction * 100, 0, 100);
                    item.DetailText = $"Installing · {fraction * 100:0}%";
                });

                var result = await installer
                    .InstallAsync(item.Plan, downloaded, installProgress, cancellation.Token)
                    .ConfigureAwait(true);

                if (result.IsSuccess)
                {
                    item.Progress = 100;
                    item.State = DownloadState.Completed;
                    item.StatusText = "Installed";
                    item.DetailText = result.Message;

                    if (!_settings.Current.KeepDownloadedPackages)
                    {
                        TryDelete(directory);
                    }

                    InstallCompleted?.Invoke(this, item.Plan.Product);
                }
                else
                {
                    item.State = DownloadState.Failed;
                    item.StatusText = result.Status == InstallStatus.NeedsAttention
                        ? "Needs attention"
                        : "Install failed";
                    item.DetailText = result.Message;
                    item.ErrorMessage = result.Message;
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            item.State = DownloadState.Cancelled;
            item.StatusText = "Cancelled";
            item.DetailText = "The download was cancelled.";
        }
        catch (Exception ex)
        {
            var message = ErrorDetails.Describe(ex);
            AppLog.Error($"Download for {item.ProductId} failed: {message}", ex);
            item.State = DownloadState.Failed;
            item.StatusText = "Failed";
            item.DetailText = message;
            item.ErrorMessage = message;
        }
        finally
        {
            _cancellations.Remove(item);
            Recalculate();
        }
    }

    private void Recalculate()
    {
        ActiveCount = Items.Count(i => i.IsActive);
        OnPropertyChanged(nameof(HasActive));
        OnPropertyChanged(nameof(HasCompleted));
        OnPropertyChanged(nameof(HasItems));
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Leftover packages can be cleaned up manually.
        }
    }

    private sealed class DispatcherProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public DispatcherProgress(Action<T> report) => _report = report;

        public void Report(T value) => Dispatcher.UIThread.Post(() => _report(value));
    }
}
