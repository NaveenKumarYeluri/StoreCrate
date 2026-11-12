namespace StoreCrate.Core.Models;

/// <summary>A lightweight product card returned by Store search.</summary>
public sealed record StoreSearchResult
{
    public required string ProductId { get; init; }

    public required string Title { get; init; }

    public string? Publisher { get; init; }

    public string? Description { get; init; }

    public double? AverageRating { get; init; }

    public int? RatingCount { get; init; }

    public string? Price { get; init; }

    public string? LogoUrl { get; init; }

    public string? TileUrl { get; init; }

    public string? BackgroundColor { get; init; }

    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PackageFamilyNames { get; init; } = Array.Empty<string>();

    public DateTimeOffset? ReleaseDate { get; init; }

    public string PublisherText => string.IsNullOrWhiteSpace(Publisher) ? "Unknown publisher" : Publisher;

    public string DescriptionText => Description ?? string.Empty;

    public string RatingText => AverageRating is null ? "Not rated" : AverageRating.Value.ToString("0.0");

    public string RatingCountText =>
        RatingCount is > 0 ? $"({RatingCount.Value:N0})" : string.Empty;

    public string PriceText => string.IsNullOrWhiteSpace(Price) ? "Free" : Price;

    public bool HasRating => AverageRating is not null;
}
