using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoreCrate.App.Services;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.App.ViewModels;

public sealed partial class SearchViewModel : ViewModelBase
{
    private readonly IStoreCatalogClient _catalog;
    private readonly NavigationService _navigation;

    public SearchViewModel(IStoreCatalogClient catalog, NavigationService navigation)
    {
        _catalog = catalog;
        _navigation = navigation;
    }

    public ObservableCollection<StoreSearchResult> Results { get; } = new();

    public IReadOnlyList<string> Suggestions { get; } =
    [
        "Spotify",
        "WhatsApp",
        "Visual Studio Code",
        "VLC",
        "PowerToys",
        "Netflix",
    ];

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private bool _hasSearched;

    [ObservableProperty]
    private string? _statusText;

    public bool HasResults => Results.Count > 0;

    public bool ShowEmptyState => HasSearched && Results.Count == 0 && !IsBusy;

    public bool ShowSuggestions => !HasSearched;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsBusy))
        {
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    partial void OnHasSearchedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSuggestions));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        var query = Query?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        IsBusy = true;
        ClearError();
        StatusText = null;

        try
        {
            var results = await _catalog.SearchAsync(query);
            Results.Clear();
            foreach (var result in results)
            {
                Results.Add(result);
            }

            HasSearched = true;
            StatusText = results.Count == 0
                ? $"No results for “{query}”"
                : $"{results.Count} result{(results.Count == 1 ? string.Empty : "s")} for “{query}”";
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(ShowSuggestions));
        }
        catch (Exception ex)
        {
            ReportError(ex);
            HasSearched = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SearchSuggestion(string? suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion))
        {
            return;
        }

        Query = suggestion;
        _ = SearchAsync();
    }

    [RelayCommand]
    private void OpenProduct(StoreSearchResult? result)
    {
        if (result is not null)
        {
            _navigation.ShowProduct(result.ProductId);
        }
    }
}
