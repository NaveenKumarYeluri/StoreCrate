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

    /// <summary>Remembers Store lookups per package family so repeated clicks are instant.</summary>
    private readonly Dictionary<string, string?> _productIdCache = new(StringComparer.OrdinalIgnoreCase);

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
        _includeNonStore = settings.Current.ShowNonStoreApps;
    }

    public ObservableCollection<InstalledAppRow> Apps { get; } = new();

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private bool _includeNonStore;

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
            await ReloadAsync();
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

    /// <summary>Re-reads the package list without the re-entrancy guard used by the command.</summary>
    private async Task ReloadAsync()
    {
        _allApps = (await _installedApps.GetInstalledAppsAsync(IncludeNonStore)).ToList();
        ApplyFilter();
        StatusText = _allApps.Count == 1 ? "1 app installed" : $"{_allApps.Count} apps installed";
    }

    internal async Task OpenRowAsync(InstalledAppRow row)
    {
        if (row.IsBusy)
        {
            return;
        }

        row.IsBusy = true;
        try
        {
            if (!await _installedApps.LaunchAsync(row.App))
            {
                StatusText = $"Windows could not launch {row.DisplayName}.";
            }
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    internal void RequestUninstall(InstalledApp? app) => PendingUninstall = app;

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
        ClearError();
        StatusText = $"Uninstalling {app.DisplayName}…";
        InstallResult? result = null;

        try
        {
            result = await UninstallRunner.RunAsync(_installedApps, app);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsBusy = false;

            // Always re-read the system list. If the package disappeared while the WinRT
            // operation was still finishing, the row is removed here instead of lingering.
            try
            {
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }

            // Keep the outcome visible; ReloadAsync overwrites StatusText with the count.
            if (result is not null)
            {
                StatusText = result.Message;
            }

            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    internal async Task ShowRowInStoreAsync(InstalledAppRow row)
    {
        if (row.IsBusy)
        {
            return;
        }

        row.IsBusy = true;
        StatusText = $"Looking up {row.DisplayName}…";

        try
        {
            var familyName = row.PackageFamilyName;

            if (!_productIdCache.TryGetValue(familyName, out var productId))
            {
                productId = await _catalog.GetProductIdByPackageFamilyAsync(familyName);
                _productIdCache[familyName] = productId;
            }

            if (productId is null)
            {
                StatusText =
                    $"{row.DisplayName} is not listed in the Microsoft Store on this device. " +
                    "It was installed outside the Store or has been removed from the catalog.";
                return;
            }

            _navigation.ShowProduct(productId);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    partial void OnIncludeNonStoreChanged(bool value)
    {
        _settings.Current.ShowNonStoreApps = value;
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
            Apps.Add(new InstalledAppRow(app, this));
        }

        OnPropertyChanged(nameof(HasApps));
        OnPropertyChanged(nameof(ShowEmptyState));
    }
}
