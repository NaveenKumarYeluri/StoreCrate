namespace StoreCrate.Core.Models;

/// <summary>Full product details as returned by the Display Catalog.</summary>
public sealed record StoreProduct
{
    public required string ProductId { get; init; }

    public required string Title { get; init; }

    public string? Publisher { get; init; }

    public string? Developer { get; init; }

    public string? ShortDescription { get; init; }

    public string? Description { get; init; }

    public string? PackageFamilyName { get; init; }

    public string? PackageIdentityName { get; init; }

    public string? PublisherId { get; init; }

    public string? WuCategoryId { get; init; }

    public string? Market { get; init; }

    public string? Locale { get; init; }

    public double? AverageRating { get; init; }

    public int? RatingCount { get; init; }

    public string? Price { get; init; }

    public DateTimeOffset? ReleaseDate { get; init; }

    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();

    public IReadOnlyList<StoreImage> Images { get; init; } = Array.Empty<StoreImage>();

    public IReadOnlyList<CatalogPackage> Packages { get; init; } = Array.Empty<CatalogPackage>();

    public string? LogoUrl => Images.FirstOrDefault(i => i.IsLogo)?.Url;

    public string? HeroImageUrl => Images.FirstOrDefault(i => i.IsTile)?.Url ?? LogoUrl;

    public IReadOnlyList<string> ScreenshotUrls =>
        Images.Where(i => i.IsScreenshot).Select(i => i.Url).ToArray();

    public string? PrimaryCategory => Categories.FirstOrDefault();

    /// <summary>The newest package version advertised by the catalog.</summary>
    public string? LatestVersionText
    {
        get
        {
            var compatible = Packages
                .Where(p => p.Platforms.Count == 0 || p.Platforms.Any(pl => pl is "Windows.Desktop" or "Windows.Universal" or "Windows.Core"))
                .ToList();
            var source = compatible.Count > 0 ? compatible : Packages;
            var versions = source
                .Select(p => Version.TryParse(p.Version, out var version) ? version : null)
                .Where(version => version is not null)
                .Select(version => version!)
                .ToList();

            return versions.Count == 0 ? null : versions.Max()!.ToString();
        }
    }

    public string PublisherText => string.IsNullOrWhiteSpace(Publisher) ? "Unknown publisher" : Publisher;

    public string SummaryText =>
        !string.IsNullOrWhiteSpace(ShortDescription)
            ? ShortDescription
            : !string.IsNullOrWhiteSpace(Description)
                ? Description
                : string.Empty;

    public string PriceText => string.IsNullOrWhiteSpace(Price) ? "Free" : Price;

    public string VersionText => LatestVersionText ?? "Unknown";

    public string RatingText => AverageRating is null ? "Not rated" : AverageRating.Value.ToString("0.0");

    public string RatingCountText => RatingCount is > 0 ? $"{RatingCount.Value:N0} ratings" : "No ratings yet";

    public string ReleaseDateText =>
        ReleaseDate is null ? "Unknown" : ReleaseDate.Value.ToString("d MMM yyyy");

    public string ArchitectureText
    {
        get
        {
            var architectures = Packages
                .SelectMany(p => p.Architectures)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return architectures.Count == 0 ? "Any" : string.Join(", ", architectures);
        }
    }

    public bool HasScreenshots => ScreenshotUrls.Count > 0;

    public long? EstimatedDownloadSizeBytes =>
        EstimateDownloadSizeBytes(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture);

    /// <summary>Estimates the download size for a specific architecture, excluding other architectures' packages.</summary>
    public long? EstimateDownloadSizeBytes(System.Runtime.InteropServices.Architecture architecture)
    {
        if (Packages.Count == 0)
        {
            return null;
        }

        var archName = architecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.X86 => "x86",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.Arm => "arm",
            _ => "x64",
        };

        var matching = Packages
            .Where(p => p.Architectures.Count == 0 ||
                        p.Architectures.Any(a =>
                            a.Equals(archName, StringComparison.OrdinalIgnoreCase) ||
                            a.Equals("neutral", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (matching.Count == 0)
        {
            matching = Packages.ToList();
        }

        var total = matching.Sum(p => p.MaxDownloadSizeBytes ?? 0);
        return total > 0 ? total : null;
    }

    public string SizeText
    {
        get
        {
            var bytes = EstimatedDownloadSizeBytes;
            return bytes is null ? "Unknown size" : FormatBytes(bytes.Value);
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.#} {units[unit]}";
    }
}
