using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StoreCrate.App.Services;
using StoreCrate.Core.Services;

namespace StoreCrate.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly NavigationService _navigation;
    private readonly IServiceProvider _services;
    private readonly IUpdateChecker _updateChecker;
    private readonly DownloadManager _downloads;
    private readonly Dictionary<NavigationTarget, ViewModelBase> _pages = new();

    private NavigationTarget _backTarget = NavigationTarget.Discover;
    private NavItem? _selectedNavItem;

    public MainWindowViewModel(
        NavigationService navigation,
        ISettingsService settings,
        IUpdateChecker updateChecker,
        DownloadManager downloads,
        IServiceProvider services)
    {
        _navigation = navigation;
        _updateChecker = updateChecker;
        _downloads = downloads;
        _services = services;

        NavItems =
        [
            new NavItem(NavigationTarget.Discover, "Discover", "\uE80F"),
            new NavItem(NavigationTarget.Search, "Search", "\uE721"),
            new NavItem(NavigationTarget.Installed, "Installed", "\uE71D"),
            new NavItem(NavigationTarget.Downloads, "Downloads", "\uE896"),
            new NavItem(NavigationTarget.Settings, "Settings", "\uE713"),
        ];

        _navigation.Requested += OnNavigationRequested;
        _navigation.BackRequested += OnBackRequested;
        _downloads.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(DownloadManager.ActiveCount) or nameof(DownloadManager.HasActive))
            {
                UpdateDownloadBadge();
            }
        };
        _downloads.InstallCompleted += (_, _) => UpdateDownloadBadge();

        NavigateTo(NavigationTarget.Discover);

        if (settings.Current.CheckForUpdatesOnStartup)
        {
            _ = CheckForUpdatesAsync();
        }

        if (settings.Current.AutomaticallyUpdateApps)
        {
            _ = AutoUpdateAppsAsync();
        }
    }

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _hasUpdate;

    [ObservableProperty]
    private string? _updateText;

    [ObservableProperty]
    private string? _updateUrl;

    [ObservableProperty]
    private bool _isInstallingUpdate;

    [ObservableProperty]
    private string? _updateStateText;

    partial void OnIsInstallingUpdateChanged(bool value) => OnPropertyChanged(nameof(InstallUpdateText));

    private AppUpdateInfo? _update;

    public string VersionText => $"StoreCrate {AppInfo.VersionString}";

    public string InstallUpdateText => IsInstallingUpdate ? "Updating…" : "Install update";

    public string RepositoryUrl => AppInfo.RepositoryUrl;

    [RelayCommand]
    private void Navigate(NavItem? item)
    {
        if (item is null)
        {
            return;
        }

        NavigateTo(item.Target);
    }

    [RelayCommand]
    private void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        NavigateTo(_backTarget, clearHistory: true);
    }

    [RelayCommand]
    private void OpenRelease()
    {
        if (!string.IsNullOrWhiteSpace(UpdateUrl))
        {
            _services.GetRequiredService<DialogService>().OpenUrl(UpdateUrl);
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (_update?.DownloadUrl is null || IsInstallingUpdate)
        {
            return;
        }

        IsInstallingUpdate = true;
        UpdateStateText = "Downloading update…";

        try
        {
            var selfUpdate = _services.GetRequiredService<SelfUpdateService>();
            await selfUpdate.DownloadAndStageAsync(_update);
            selfUpdate.ApplyOnExit();
            UpdateStateText = "Update ready. StoreCrate will restart.";
            await Task.Delay(600);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            UpdateStateText = $"Update failed: {ex.Message}";
            IsInstallingUpdate = false;
        }
    }

    [RelayCommand]
    private void OpenRepository() =>
        _services.GetRequiredService<DialogService>().OpenUrl(RepositoryUrl);

    private void OnNavigationRequested(object? sender, NavigationRequest request)
    {
        if (request.Target == NavigationTarget.Details && request.ProductId is { } productId)
        {
            _ = ShowDetailsAsync(productId);
            return;
        }

        NavigateTo(request.Target);
    }

    private void OnBackRequested(object? sender, EventArgs args) => GoBack();

    private async Task ShowDetailsAsync(string productId)
    {
        var details = _services.GetRequiredService<DetailsViewModel>();
        if (_selectedNavItem is not null)
        {
            _backTarget = _selectedNavItem.Target;
        }

        CanGoBack = true;
        CurrentPage = details;

        try
        {
            await details.LoadAsync(productId);
        }
        catch (Exception ex)
        {
            details.ReportError(ex);
        }
    }

    private void NavigateTo(NavigationTarget target, bool clearHistory = false)
    {
        if (clearHistory)
        {
            CanGoBack = false;
        }

        if (_selectedNavItem is not null)
        {
            _selectedNavItem.IsSelected = false;
        }

        var navItem = NavItems.FirstOrDefault(i => i.Target == target);
        if (navItem is not null)
        {
            navItem.IsSelected = true;
            _selectedNavItem = navItem;
            _backTarget = target;
        }

        var page = GetPage(target);
        CurrentPage = page;
        _ = NotifyNavigatedAsync(page);
    }

    private ViewModelBase GetPage(NavigationTarget target)
    {
        if (_pages.TryGetValue(target, out var cached))
        {
            return cached;
        }

        var page = target switch
        {
            NavigationTarget.Search => (ViewModelBase)_services.GetRequiredService<SearchViewModel>(),
            NavigationTarget.Installed => _services.GetRequiredService<InstalledViewModel>(),
            NavigationTarget.Downloads => _services.GetRequiredService<DownloadsViewModel>(),
            NavigationTarget.Settings => _services.GetRequiredService<SettingsViewModel>(),
            _ => _services.GetRequiredService<DiscoverViewModel>(),
        };

        _pages[target] = page;
        return page;
    }

    private static async Task NotifyNavigatedAsync(ViewModelBase page)
    {
        try
        {
            await page.OnNavigatedToAsync();
        }
        catch
        {
            // Pages surface their own errors.
        }
    }

    private void UpdateDownloadBadge()
    {
        var downloads = NavItems.FirstOrDefault(i => i.Target == NavigationTarget.Downloads);
        if (downloads is not null)
        {
            downloads.Badge = _downloads.ActiveCount;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var update = await _updateChecker.CheckAsync();
            if (update is null)
            {
                return;
            }

            _update = update;
            UpdateText = $"Version {update.Tag} is available";
            UpdateUrl = update.ReleaseUrl;
            HasUpdate = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks every installed Store app for a newer catalog version and queues the
    /// downloads, mirroring the Microsoft Store's automatic update behavior.
    /// </summary>
    private async Task AutoUpdateAppsAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5));

            var installed = _services.GetRequiredService<IInstalledAppService>();
            var appUpdates = _services.GetRequiredService<IAppUpdateService>();
            var fe3 = _services.GetRequiredService<IFe3Client>();
            var resolver = _services.GetRequiredService<IPackageResolver>();

            var apps = await installed.GetInstalledAppsAsync();
            var updates = await appUpdates.CheckForUpdatesAsync(apps);
            if (updates.Count == 0)
            {
                return;
            }

            var queued = 0;
            foreach (var update in updates)
            {
                if (string.IsNullOrWhiteSpace(update.Product.WuCategoryId))
                {
                    continue;
                }

                var packages = await fe3.GetPackagesAsync(update.Product.WuCategoryId);
                var plan = resolver.Resolve(update.Product, packages);
                _downloads.Enqueue(plan);
                queued++;
            }

            if (queued > 0)
            {
                Debug.WriteLine($"Queued {queued} app update(s).");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App update check failed: {ex.Message}");
        }
    }
}
