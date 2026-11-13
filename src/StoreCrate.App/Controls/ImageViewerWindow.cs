using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StoreCrate.App.Services;

namespace StoreCrate.App.Controls;

/// <summary>
/// Full-size image viewer shown inside the app when a screenshot is clicked.
/// Supports wheel zoom, drag-to-pan, double-click zoom reset, and arrow-key
/// navigation through the product's screenshot gallery.
/// </summary>
public sealed class ImageViewerWindow : Window
{
    private const double ZoomStep = 1.2;
    private const double MaxZoom = 6.0;

    private readonly Image _image;
    private readonly TextBlock _counter;
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly TranslateTransform _pan = new(0, 0);
    private readonly List<string> _urls;
    private int _index;
    private Point _dragOrigin;
    private bool _dragging;

    public ImageViewerWindow(IReadOnlyList<string> urls, int startIndex)
    {
        _urls = [.. urls];
        _index = Math.Clamp(startIndex, 0, Math.Max(0, _urls.Count - 1));

        Title = "StoreCrate - Screenshot";
        Width = 1120;
        Height = 780;
        MinWidth = 560;
        MinHeight = 420;
        Background = new SolidColorBrush(Color.FromRgb(24, 24, 28));
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

        _image = new Image
        {
            Stretch = Stretch.Uniform,
            RenderTransform = new TransformGroup
            {
                Children = { _zoom, _pan },
            },
        };

        _counter = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
        };

        var previous = MakeNavButton("<", () => Step(-1));
        var next = MakeNavButton(">", () => Step(1));

        var counterPanel = new Border
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 6),
            Margin = new Thickness(0, 0, 0, 14),
            Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 24)),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 14,
                Children = { previous, _counter, next },
            },
        };

        var closeButton = new Button
        {
            Content = "X",
            Width = 36,
            Height = 36,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 12, 0),
            Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 24)),
            Foreground = Brushes.White,
            FontSize = 14,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(18),
        };
        closeButton.Click += (_, _) => Close();

        var layer = new Panel();
        layer.Children.Add(_image);
        layer.Children.Add(counterPanel);
        layer.Children.Add(closeButton);

        Content = layer;

        PointerWheelChanged += OnViewerWheel;
        PointerPressed += OnViewerPressed;
        PointerMoved += OnViewerMoved;
        PointerReleased += OnViewerReleased;
        DoubleTapped += OnViewerDoubleTapped;
        KeyDown += OnViewerKeyDown;

        Closed += (_, _) => _image.Source = null;

        ShowCurrent();
    }

    private Button MakeNavButton(string glyph, Action action)
    {
        var button = new Button
        {
            Content = glyph,
            Width = 34,
            Height = 30,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void ShowCurrent()
    {
        _counter.Text = _urls.Count == 0 ? string.Empty : $"{_index + 1} / {_urls.Count}";
        _zoom.ScaleX = _zoom.ScaleY = 1;
        _pan.X = _pan.Y = 0;

        var url = _index >= 0 && _index < _urls.Count ? _urls[_index] : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            _image.Source = null;
            return;
        }

        Title = $"StoreCrate - Screenshot {_index + 1} of {_urls.Count}";

        // Fire and forget: the cache deduplicates requests and the callback
        // verifies the current index before touching the image.
        _ = LoadAsync(url);
    }

    private async Task LoadAsync(string url)
    {
        Bitmap? bitmap;
        try
        {
            bitmap = await AppServices.Get<ImageCache>().GetAsync(url);
        }
        catch
        {
            bitmap = null;
        }

        if (_index < 0 || _index >= _urls.Count || !string.Equals(_urls[_index], url, StringComparison.Ordinal))
        {
            return;
        }

        if (bitmap is null)
        {
            _image.Source = null;
            _counter.Text = $"{_index + 1} / {_urls.Count} (could not load)";
            return;
        }

        _image.Source = bitmap;
    }

    private void Step(int delta)
    {
        if (_urls.Count == 0)
        {
            return;
        }

        var next = (_index + delta + _urls.Count) % _urls.Count;
        if (next == _index)
        {
            return;
        }

        _index = next;
        ShowCurrent();
    }

    private void OnViewerWheel(object? sender, PointerWheelEventArgs args)
    {
        var delta = args.Delta.Y;
        if (delta == 0)
        {
            return;
        }

        var factor = delta > 0 ? ZoomStep : 1 / ZoomStep;
        var zoomed = Math.Clamp(_zoom.ScaleX * factor, 1.0, MaxZoom);
        _zoom.ScaleX = _zoom.ScaleY = zoomed;

        if (zoomed <= 1.0)
        {
            _pan.X = _pan.Y = 0;
        }

        args.Handled = true;
    }

    private void OnViewerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (_zoom.ScaleX <= 1.0)
        {
            return;
        }

        _dragging = true;
        _dragOrigin = args.GetCurrentPoint(this).Position - new Point(_pan.X, _pan.Y);
    }

    private void OnViewerMoved(object? sender, PointerEventArgs args)
    {
        if (!_dragging)
        {
            return;
        }

        var position = args.GetCurrentPoint(this).Position - _dragOrigin;
        _pan.X = position.X;
        _pan.Y = position.Y;
    }

    private void OnViewerReleased(object? sender, PointerReleasedEventArgs args) => _dragging = false;

    private void OnViewerDoubleTapped(object? sender, TappedEventArgs args)
    {
        if (_zoom.ScaleX > 1.0)
        {
            _zoom.ScaleX = _zoom.ScaleY = 1;
            _pan.X = _pan.Y = 0;
        }
        else
        {
            _zoom.ScaleX = _zoom.ScaleY = 2;
        }

        args.Handled = true;
    }

    private void OnViewerKeyDown(object? sender, KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Key.Escape:
                Close();
                args.Handled = true;
                break;
            case Key.Left or Key.PageUp:
                Step(-1);
                args.Handled = true;
                break;
            case Key.Right or Key.PageDown:
                Step(1);
                args.Handled = true;
                break;
        }
    }
}
