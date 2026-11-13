using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

/// <summary>Reads products and search results from the public Microsoft Store catalog endpoints.</summary>
public interface IStoreCatalogClient
{
    Task<IReadOnlyList<StoreSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<StoreProduct?> GetProductAsync(string productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the Store product id for an installed package family name, or null when the
    /// package family is not listed in the catalog for the current market.
    /// </summary>
    Task<string?> GetProductIdByPackageFamilyAsync(string packageFamilyName, CancellationToken cancellationToken = default);
}
