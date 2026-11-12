using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

/// <summary>Resolves real, time-limited download URLs for a product's packages via the FE3 delivery service.</summary>
public interface IFe3Client
{
    Task<IReadOnlyList<Fe3Package>> GetPackagesAsync(string wuCategoryId, CancellationToken cancellationToken = default);
}
