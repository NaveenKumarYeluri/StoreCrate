namespace StoreCrate.Core.Models;

/// <summary>The set of packages required to install a Store product.</summary>
public sealed record InstallPlan
{
    public required StoreProduct Product { get; init; }

    public required Fe3Package MainPackage { get; init; }

    public IReadOnlyList<Fe3Package> Dependencies { get; init; } = Array.Empty<Fe3Package>();

    /// <summary>Dependencies first, then the main package.</summary>
    public IReadOnlyList<Fe3Package> AllPackages =>
        Dependencies.Concat([MainPackage]).ToArray();
}
