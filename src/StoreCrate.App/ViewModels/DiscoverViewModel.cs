using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoreCrate.App.Services;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.App.ViewModels;

public sealed record DiscoverSection(string Title, IReadOnlyList<StoreSearchResult> Items);

public sealed partial class DiscoverViewModel : ViewModelBase
{
    private const string FeaturedProductId = "9NCBCSZSJRSB";

    private static readonly (string Title, string Query)[] SectionQueries =
    [
        ("Music & audio", "music"),
        ("Social & messaging", "social"),
        ("Productivity", "productivity"),
        ("Games", "games"),
        ("Photo & video", "photo video"),
        ("Utilities", "utility"),
    ];

    private readonly IStoreCatalogClient _catalog;
    private readonly NavigationService _navigation;
    private bool _loaded;

    public DiscoverViewModel(IStoreCatalogClient catalog, NavigationService navigation)
    {
        _catalog = catalog;
        _navigation = navigation;
    }

    public ObservableCollection<DiscoverSection> Sections { get; } = new();

    [ObservableProperty]
    private StoreProduct? _featured;

    [ObservableProperty]
    private bool _hasLoaded;

    public bool HasFeatured => Featured is not null;

    public bool HasSections => Sections.Count > 0;

    public override Task OnNavigatedToAsync()
    {
        if (!_loaded && !IsBusy)
        {
            _ = LoadAsync();
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            var featuredTask = _catalog.GetProductAsync(FeaturedProductId);
            var searches = SectionQueries
                .Select(section => _catalog.SearchAsync(section.Query))
                .ToArray();

            var featured = await featuredTask;
            var results = await Task.WhenAll(searches);

            Featured = featured;
            OnPropertyChanged(nameof(HasFeatured));

            Sections.Clear();
            for (var index = 0; index < SectionQueries.Length; index++)
            {
                var items = results[index]
                    .Where(item => !string.Equals(
                        item.ProductId,
                        featured?.ProductId,
                        StringComparison.OrdinalIgnoreCase))
                    .GroupBy(item => item.ProductId)
                    .Select(group => group.First())
                    .Take(10)
                    .ToList();

                if (items.Count > 0)
                {
                    Sections.Add(new DiscoverSection(SectionQueries[index].Title, items));
                }
            }

            OnPropertyChanged(nameof(HasSections));
            HasLoaded = true;
            _loaded = true;
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

    [RelayCommand]
    private void OpenProduct(StoreSearchResult? result)
    {
        if (result is not null)
        {
            _navigation.ShowProduct(result.ProductId);
        }
    }

    [RelayCommand]
    private void OpenFeatured()
    {
        if (Featured is not null)
        {
            _navigation.ShowProduct(Featured.ProductId);
        }
    }

    [RelayCommand]
    private void Retry() => _ = LoadAsync();
}
