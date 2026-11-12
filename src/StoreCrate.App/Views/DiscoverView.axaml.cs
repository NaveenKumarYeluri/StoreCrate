using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StoreCrate.App.Views;

public partial class DiscoverView : UserControl
{
    public DiscoverView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
