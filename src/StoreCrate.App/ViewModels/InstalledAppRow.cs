using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoreCrate.Core.Models;
namespace StoreCrate.App.ViewModels;

/// <summary>
/// One row on the Installed page. Each row owns its own commands so that running an
/// action for one app never disables or highlights the buttons of every other app.
/// </summary>
public sealed partial class InstalledAppRow : ObservableObject
{
    private readonly InstalledViewModel _owner;

    public InstalledAppRow(InstalledApp app, InstalledViewModel owner)
    {
        App = app;
        _owner = owner;
    }

    public InstalledApp App { get; }

    public string DisplayName => App.DisplayName;

    public string PublisherText => App.PublisherText;

    public string PackageFamilyName => App.PackageFamilyName;

    public string VersionText => App.VersionText;

    public string Initial => App.Initial;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasUpdate;

    [ObservableProperty]
    private string? _updateVersionText;

    /// <summary>The catalog product that offers a newer version, set by the update check.</summary>
    public StoreProduct? UpdateProduct { get; set; }

    [RelayCommand]
    private Task OpenAsync() => _owner.OpenRowAsync(this);

    [RelayCommand]
    private Task ShowInStoreAsync() => _owner.ShowRowInStoreAsync(this);

    [RelayCommand]
    private Task UpdateAsync() => _owner.UpdateRowAsync(this);

    [RelayCommand]
    private void Uninstall() => _owner.RequestUninstall(App);
}
