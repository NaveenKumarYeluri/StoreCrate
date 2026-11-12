using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using StoreCrate.App.Services;

namespace StoreCrate.App.Controls;

/// <summary>An <see cref="Image"/> that loads its bitmap asynchronously from a URL.</summary>
public sealed class AsyncImage : Image
{
    public static readonly StyledProperty<string?> SourceUrlProperty =
        AvaloniaProperty.Register<AsyncImage, string?>(nameof(SourceUrl));

    static AsyncImage()
    {
        SourceUrlProperty.Changed.AddClassHandler<AsyncImage>((image, args) => image.OnSourceUrlChanged(args));
    }

    public string? SourceUrl
    {
        get => GetValue(SourceUrlProperty);
        set => SetValue(SourceUrlProperty, value);
    }

    private async void OnSourceUrlChanged(AvaloniaPropertyChangedEventArgs args)
    {
        var url = args.NewValue as string;
        if (string.IsNullOrWhiteSpace(url))
        {
            Source = null;
            return;
        }

        Bitmap? bitmap;
        try
        {
            bitmap = await AppServices.Get<ImageCache>().GetAsync(url);
        }
        catch
        {
            return;
        }

        if (bitmap is not null && string.Equals(SourceUrl, url, StringComparison.Ordinal))
        {
            Source = bitmap;
        }
    }
}
