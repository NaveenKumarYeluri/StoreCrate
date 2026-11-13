using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoreCrate.App.Services;
using StoreCrate.Core.Logging;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.App.ViewModels;

public sealed partial class DetailsViewModel : ViewModelBase
{
    private readonly IStoreCatalogClient _catalog;
    private readonly IInstalledAppService _installedApps;
    private readonly IFe3Client _fe3;
    private readonly IPackageResolver _resolver;
    private readonly DownloadManager _downloads;
    private readonly NavigationService _navigation;

    public DetailsViewModel(
        IStoreCatalogClient catalog,
        IInstalledAppService installedApps,
        IFe3Client fe3,
        IPackageResolver resolver,
        DownloadManager downloads,
        NavigationService navigation)
    {
        _catalog = catalog;
        _installedApps = installedApps;
        _fe3 = fe3;
        _resolver = resolver;
        _downloads = downloads;
        _navigation = navigation;
    }

    [ObservableProperty]
    private StoreProduct? _product;

    [ObservableProperty]
    private InstalledApp? _installedApp;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    private bool _showUninstallConfirm;

    public string ProductId { get; private set; } = string.Empty;

    public bool HasProduct => Product is not null;

    public bool HasScreenshots => Product?.HasScreenshots == true;

    public string InstallButtonText => IsInstalled ? "Reinstall" : "Install";

    public string InstalledVersionText => InstalledApp?.VersionText ?? string.Empty;

    public async Task LoadAsync(string productId)
    {
        ProductId = productId;
        IsBusy = true;
        ClearError();
        StatusMessage = null;

        try
        {
            Product = await _catalog.GetProductAsync(productId);
            OnPropertyChanged(nameof(HasProduct));
            OnPropertyChanged(nameof(HasScreenshots));
            OnPropertyChanged(nameof(InstallButtonText));

            if (Product is null)
            {
                ErrorMessage = "This app could not be found in the Microsoft Store.";
                return;
            }

            await RefreshInstalledStateAsync();
        }
        catch (Exception ex)
        {
            Core.Logging.AppLog.Error($"Details load for product {productId} failed.", ex);
            ReportError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (Product is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusIsError = false;
        StatusMessage = "Resolving the best packages for this device…";

        try
        {
            if (string.IsNullOrWhiteSpace(Product.WuCategoryId))
            {
                throw new StoreApiException("This app does not expose installable packages.");
            }

            var packages = await _fe3.GetPackagesAsync(Product.WuCategoryId);
            var plan = _resolver.Resolve(Product, packages);

            _downloads.Enqueue(plan);
            StatusMessage = $"Added {plan.AllPackages.Count} package(s) to the download queue.";
            _navigation.Navigate(NavigationTarget.Downloads);
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = ErrorDetails.Describe(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (InstalledApp is null)
        {
            return;
        }

        var launched = await _installedApps.LaunchAsync(InstalledApp);
        if (!launched)
        {
            StatusIsError = true;
            StatusMessage = "Windows could not launch this app.";
        }
    }

    [RelayCommand]
    private void RequestUninstall()
    {
        if (IsInstalled)
        {
            ShowUninstallConfirm = true;
        }
    }

    [RelayCommand]
    private void CancelUninstall() => ShowUninstallConfirm = false;

    [RelayCommand]
    private async Task ConfirmUninstallAsync()
    {
        if (InstalledApp is null)
        {
            return;
        }

        ShowUninstallConfirm = false;
        IsBusy = true;

        try
        {
            var result = await UninstallRunner.RunAsync(_installedApps, InstalledApp);
            StatusIsError = !result.IsSuccess;
            StatusMessage = result.Message;
            await RefreshInstalledStateAsync();
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = ErrorDetails.Describe(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoBack() => _navigation.Back();

    [RelayCommand]
    private Task RetryAsync() => LoadAsync(ProductId);

    private async Task RefreshInstalledStateAsync()
    {
        if (Product?.PackageFamilyName is not { } familyName)
        {
            InstalledApp = null;
            IsInstalled = false;
            return;
        }

        InstalledApp = await _installedApps.FindInstalledAsync(familyName);
        IsInstalled = InstalledApp is not null;
        OnPropertyChanged(nameof(InstallButtonText));
        OnPropertyChanged(nameof(InstalledVersionText));
    }
}
