namespace StoreCrate.Core.Models;

/// <summary>A downloadable package as advertised by the Store catalog (not the delivery service).</summary>
public sealed record CatalogPackage
{
    public IReadOnlyList<string> Architectures { get; init; } = Array.Empty<string>();

    public string? Version { get; init; }

    public long? MaxDownloadSizeBytes { get; init; }

    public long? MaxInstallSizeBytes { get; init; }

    public string? PackageFormat { get; init; }

    public string? PackageFullName { get; init; }

    public string? PackageFamilyName { get; init; }
}
