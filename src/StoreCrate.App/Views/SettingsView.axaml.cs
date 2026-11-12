using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StoreCrate.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
