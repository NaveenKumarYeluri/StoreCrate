using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoreCrate.App.Services;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IUpdateChecker _updateChecker;
    private readonly DialogService _dialogs;

    public SettingsViewModel(
        ISettingsService settings,
        IUpdateChecker updateChecker,
        DialogService dialogs)
    {
        _settings = settings;
        _updateChecker = updateChecker;
        _dialogs = dialogs;

        var current = settings.Current;
        _theme = current.Theme;
        _market = current.Market;
        _locale = current.Locale;
        _downloadDirectory = settings.DownloadDirectory;
        _keepDownloadedPackages = current.KeepDownloadedPackages;
        _checkForUpdatesOnStartup = current.CheckForUpdatesOnStartup;
        _showSystemApps = current.ShowSystemApps;
        _maxConcurrentDownloads = current.MaxConcurrentDownloads;
    }

    public IReadOnlyList<string> Themes { get; } = ["System", "Light", "Dark"];

    public IReadOnlyList<int> ConcurrencyOptions { get; } = [1, 2, 3, 4];

    public string VersionText => AppInfo.VersionString;

    public string RepositoryUrl => AppInfo.RepositoryUrl;

    public string DefaultDownloadDirectory => AppPaths.DefaultDownloadDirectory;

    [ObservableProperty]
    private string _theme;

    [ObservableProperty]
    private string _market;

    [ObservableProperty]
    private string _locale;

    [ObservableProperty]
    private string _downloadDirectory;

    [ObservableProperty]
    private bool _keepDownloadedPackages;

    [ObservableProperty]
    private bool _checkForUpdatesOnStartup;

    [ObservableProperty]
    private bool _showSystemApps;

    [ObservableProperty]
    private int _maxConcurrentDownloads;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    private string? _updateStatusText;

    public override Task OnNavigatedToAsync()
    {
        LoadFromSettings();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = _settings.Current;

        settings.Market = string.IsNullOrWhiteSpace(Market) ? "US" : Market.Trim().ToUpperInvariant();
        settings.Locale = string.IsNullOrWhiteSpace(Locale) ? "en-US" : Locale.Trim();
        settings.Theme = Theme;
        settings.DownloadDirectory = IsDefaultDirectory(DownloadDirectory) ? null : DownloadDirectory.Trim();
        settings.KeepDownloadedPackages = KeepDownloadedPackages;
        settings.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;
        settings.ShowSystemApps = ShowSystemApps;
        settings.MaxConcurrentDownloads = Math.Clamp(MaxConcurrentDownloads, 1, 4);

        await _settings.SaveAsync();
        App.ApplyTheme(settings.Theme);

        Market = settings.Market;
        Locale = settings.Locale;
        DownloadDirectory = _settings.DownloadDirectory;
        StatusIsError = false;
        StatusText = "Settings saved.";
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync(DownloadDirectory);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            DownloadDirectory = folder;
        }
    }

    [RelayCommand]
    private void UseDefaultFolder() => DownloadDirectory = AppPaths.DefaultDownloadDirectory;

    [RelayCommand]
    private void OpenDownloadsFolder() => _dialogs.OpenInExplorer(DownloadDirectory);

    [RelayCommand]
    private void OpenRepository() => _dialogs.OpenUrl(RepositoryUrl);

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        UpdateStatusText = "Checking GitHub Releases…";

        try
        {
            var update = await _updateChecker.CheckAsync();
            UpdateStatusText = update is null
                ? $"You're running the latest version ({AppInfo.VersionString})."
                : $"Version {update.Tag} is available.";
        }
        catch (Exception ex)
        {
            UpdateStatusText = $"Update check failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        var defaults = new AppSettings();
        _settings.Current.Market = defaults.Market;
        _settings.Current.Locale = defaults.Locale;
        _settings.Current.Theme = defaults.Theme;
        _settings.Current.DownloadDirectory = defaults.DownloadDirectory;
        _settings.Current.KeepDownloadedPackages = defaults.KeepDownloadedPackages;
        _settings.Current.CheckForUpdatesOnStartup = defaults.CheckForUpdatesOnStartup;
        _settings.Current.ShowSystemApps = defaults.ShowSystemApps;
        _settings.Current.MaxConcurrentDownloads = defaults.MaxConcurrentDownloads;

        await _settings.SaveAsync();
        App.ApplyTheme(defaults.Theme);
        LoadFromSettings();

        StatusIsError = false;
        StatusText = "Settings restored to defaults.";
    }

    partial void OnThemeChanged(string value) => App.ApplyTheme(value);

    private void LoadFromSettings()
    {
        var current = _settings.Current;
        Theme = current.Theme;
        Market = current.Market;
        Locale = current.Locale;
        DownloadDirectory = _settings.DownloadDirectory;
        KeepDownloadedPackages = current.KeepDownloadedPackages;
        CheckForUpdatesOnStartup = current.CheckForUpdatesOnStartup;
        ShowSystemApps = current.ShowSystemApps;
        MaxConcurrentDownloads = current.MaxConcurrentDownloads;
        StatusText = null;
        UpdateStatusText = null;
    }

    private static bool IsDefaultDirectory(string? path) =>
        string.Equals(
            path?.Trim().TrimEnd(Path.DirectorySeparatorChar),
            AppPaths.DefaultDownloadDirectory.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
