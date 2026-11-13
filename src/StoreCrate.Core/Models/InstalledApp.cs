namespace StoreCrate.Core.Models;

/// <summary>An app package installed for the current Windows user.</summary>
public sealed record InstalledApp
{
    public required string DisplayName { get; init; }

    public required string PackageFullName { get; init; }

    public required string PackageFamilyName { get; init; }

    public string? Version { get; init; }

    public string? Publisher { get; init; }

    public string? InstallLocation { get; init; }

    /// <summary>True when the package was not signed by the Microsoft Store (system components, enterprise or sideloaded packages).</summary>
    public bool IsNonStore { get; init; }

    public string? ProductId { get; init; }

    public string Initial =>
        string.IsNullOrWhiteSpace(DisplayName) ? "?" : DisplayName.Trim()[..1].ToUpperInvariant();

    public string PublisherText => string.IsNullOrWhiteSpace(Publisher) ? "Unknown publisher" : Publisher;

    public string VersionText => string.IsNullOrWhiteSpace(Version) ? "Unknown" : Version;
}
