using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using StoreCrate.App.Services;

namespace StoreCrate.App.Views;

public partial class DetailsView : UserControl
{
    private const double WheelStep = 110.0;
    private const double ButtonStep = 404.0; // screenshot width (380) + spacing

    public DetailsView()
    {
        InitializeComponent();

        // Do not touch named controls here: they are not available until the control
        // is attached, and accessing them in the constructor would throw and prevent
        // the details page from ever rendering.
    }

    /// <summary>
    /// Resolved lazily from the visual tree; named controls are not reliably available
    /// before the view is attached, so handlers look the strip up on demand.
    /// </summary>
    private ScrollViewer? ScreenshotsScroll =>
        this.GetVisualDescendants().OfType<ScrollViewer>()
            .FirstOrDefault(viewer => viewer.Classes.Contains("horizontal"));

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnScreenshotsWheel(object? sender, PointerWheelEventArgs args)
    {
        var delta = args.Delta.Y + args.Delta.X;
        if (delta == 0)
        {
            return;
        }

        ScrollBy(-delta * WheelStep);
        args.Handled = true;
    }

    private void OnScrollLeftClick(object? sender, RoutedEventArgs args)
    {
        ScrollBy(-ButtonStep);
    }

    private void OnScrollRightClick(object? sender, RoutedEventArgs args)
    {
        ScrollBy(ButtonStep);
    }

    private void OnScreenshotTapped(object? sender, TappedEventArgs args)
    {
        if (sender is Control { DataContext: string url })
        {
            AppServices.Get<DialogService>().OpenUrl(url);
        }
    }

    private void ScrollBy(double amount)
    {
        if (ScreenshotsScroll is not { } scroll)
        {
            return;
        }

        var max = Math.Max(0, scroll.Extent.Width - scroll.Viewport.Width);
        var x = Math.Clamp(scroll.Offset.X + amount, 0, max);
        scroll.Offset = new Vector(x, 0);
    }
}
