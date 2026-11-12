using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StoreCrate.App.Controls;

public partial class BrandMark : UserControl
{
    public BrandMark()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
