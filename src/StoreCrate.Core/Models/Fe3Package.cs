namespace StoreCrate.Core.Models;

/// <summary>A package resolved from the FE3 delivery service, including its download URL.</summary>
public sealed record Fe3Package
{
    public required string UpdateId { get; init; }

    public required string Revision { get; init; }

    public required string Moniker { get; init; }

    public string? PackageType { get; init; }

    public required string Url { get; init; }

    private PackageMoniker? _parsed;
    private PackageMoniker ParsedCached => _parsed ??= PackageMoniker.Parse(Moniker);

    public PackageMoniker Parsed => ParsedCached;

    public string Name => ParsedCached.Name;

    public string Architecture => ParsedCached.Architecture;

    public Version? Version => ParsedCached.Version;
}
