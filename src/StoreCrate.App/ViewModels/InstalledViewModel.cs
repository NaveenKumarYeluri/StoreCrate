using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoreCrate.App.Services;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.App.ViewModels;

public sealed partial class InstalledViewModel : ViewModelBase
{
    private readonly IInstalledAppService _installedApps;
    private readonly IStoreCatalogClient _catalog;
    private readonly ISettingsService _settings;
    private readonly NavigationService _navigation;
    private List<InstalledApp> _allApps = new();

    public InstalledViewModel(
        IInstalledAppService installedApps,
        IStoreCatalogClient catalog,
        ISettingsService settings,
        NavigationService navigation)
    {
        _installedApps = installedApps;
        _catalog = catalog;
        _settings = settings;
        _navigation = navigation;
        _includeSystem = settings.Current.ShowSystemApps;
    }

    public ObservableCollection<InstalledApp> Apps { get; } = new();

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private bool _includeSystem;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private InstalledApp? _pendingUninstall;

    public bool HasApps => Apps.Count > 0;

    public bool ShowEmptyState => !IsBusy && Apps.Count == 0;

    public override async Task OnNavigatedToAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            _allApps = (await _installedApps.GetInstalledAppsAsync(IncludeSystem)).ToList();
            ApplyFilter();
            StatusText = _allApps.Count == 1 ? "1 app installed" : $"{_allApps.Count} apps installed";
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    [RelayCommand]
    private async Task LaunchAsync(InstalledApp? app)
    {
        if (app is null)
        {
            return;
        }

        if (!await _installedApps.LaunchAsync(app))
        {
            StatusText = $"Windows could not launch {app.DisplayName}.";
        }
    }

    [RelayCommand]
    private void RequestUninstall(InstalledApp? app) => PendingUninstall = app;

    [RelayCommand]
    private void CancelUninstall() => PendingUninstall = null;

    [RelayCommand]
    private async Task ConfirmUninstallAsync()
    {
        var app = PendingUninstall;
        PendingUninstall = null;
        if (app is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _installedApps.UninstallAsync(app);
            StatusText = result.Message;
            _allApps.Remove(app);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    [RelayCommand]
    private async Task FindInStoreAsync(InstalledApp? app)
    {
        if (app is null)
        {
            return;
        }

        IsBusy = true;
        StatusText = $"Looking up {app.DisplayName} in the Store…";

        try
        {
            var results = await _catalog.SearchAsync(app.DisplayName);
            var match = results.FirstOrDefault(result =>
                            result.PackageFamilyNames.Any(familyName =>
                                string.Equals(familyName, app.PackageFamilyName, StringComparison.OrdinalIgnoreCase)))
                        ?? results.FirstOrDefault();

            if (match is null)
            {
                StatusText = $"{app.DisplayName} was not found in the Microsoft Store.";
                return;
            }

            _navigation.ShowProduct(match.ProductId);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    partial void OnIncludeSystemChanged(bool value)
    {
        _settings.Current.ShowSystemApps = value;
        _ = _settings.SaveAsync();
        _ = RefreshAsync();
    }

    private void ApplyFilter()
    {
        var filter = Filter?.Trim();

        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allApps
            : _allApps
                .Where(app =>
                    app.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                    app.PackageFamilyName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    app.PublisherText.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

        Apps.Clear();
        foreach (var app in filtered)
        {
            Apps.Add(app);
        }

        OnPropertyChanged(nameof(HasApps));
        OnPropertyChanged(nameof(ShowEmptyState));
    }
}
