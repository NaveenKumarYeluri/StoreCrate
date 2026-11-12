using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

/// <summary>Chooses the main package and dependencies that match the host architecture.</summary>
public interface IPackageResolver
{
    InstallPlan Resolve(StoreProduct product, IReadOnlyList<Fe3Package> packages);
}
