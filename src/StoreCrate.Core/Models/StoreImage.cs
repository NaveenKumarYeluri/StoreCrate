namespace StoreCrate.Core.Models;

/// <summary>A single image associated with a Store product.</summary>
public sealed record StoreImage(string Purpose, string Url, int Width, int Height)
{
    public bool IsLogo => Purpose.Contains("Logo", StringComparison.OrdinalIgnoreCase);

    public bool IsScreenshot => Purpose.Contains("Screenshot", StringComparison.OrdinalIgnoreCase);

    public bool IsTile => Purpose.Contains("Tile", StringComparison.OrdinalIgnoreCase);

    /// <summary>Normalises protocol-relative image URLs returned by the Store.</summary>
    public static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        url = url.Trim();
        return url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;
    }
}
