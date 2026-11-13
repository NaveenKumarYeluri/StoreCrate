namespace StoreCrate.Core.Models;

/// <summary>The set of packages required to install a Store product.</summary>
public sealed record InstallPlan
{
    public required StoreProduct Product { get; init; }

    public required Fe3Package MainPackage { get; init; }

    public IReadOnlyList<Fe3Package> Dependencies { get; init; } = Array.Empty<Fe3Package>();

    private IReadOnlyList<Fe3Package>? _allPackages;

    /// <summary>Dependencies first, then the main package.</summary>
    public IReadOnlyList<Fe3Package> AllPackages =>
        _allPackages ??= Dependencies.Concat([MainPackage]).ToList();
}
