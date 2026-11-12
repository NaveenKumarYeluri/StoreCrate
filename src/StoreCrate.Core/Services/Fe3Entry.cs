namespace StoreCrate.Core.Services;

/// <summary>A raw update entry parsed from the FE3 sync response.</summary>
public sealed record Fe3Entry(string UpdateId, string Revision, string Moniker, string PackageType);
