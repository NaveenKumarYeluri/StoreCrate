using CommunityToolkit.Mvvm.ComponentModel;
using StoreCrate.App.Services;

namespace StoreCrate.App.ViewModels;

public sealed partial class NavItem : ObservableObject
{
    public NavItem(NavigationTarget target, string label, string glyph)
    {
        Target = target;
        Label = label;
        Glyph = glyph;
    }

    public NavigationTarget Target { get; }

    public string Label { get; }

    public string Glyph { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _badge;

    public bool HasBadge => Badge > 0;

    partial void OnBadgeChanged(int value) => OnPropertyChanged(nameof(HasBadge));
}
