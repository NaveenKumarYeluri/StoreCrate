using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StoreCrate.App.Views;

public partial class InstalledView : UserControl
{
    public InstalledView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
