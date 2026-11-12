namespace StoreCrate.Core.Models;

/// <summary>A package resolved from the FE3 delivery service, including its download URL.</summary>
public sealed record Fe3Package
{
    public required string UpdateId { get; init; }

    public required string Revision { get; init; }

    public required string Moniker { get; init; }

    public string? PackageType { get; init; }

    public required string Url { get; init; }

    public PackageMoniker Parsed => PackageMoniker.Parse(Moniker);

    public string Name => Parsed.Name;

    public string Architecture => Parsed.Architecture;

    public Version? Version => Parsed.Version;
}
