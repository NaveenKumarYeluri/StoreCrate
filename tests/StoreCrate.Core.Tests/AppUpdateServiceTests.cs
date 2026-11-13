using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.Core.Tests;

public class AppUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_DetectsNewerCatalogVersion()
    {
        var catalog = new FakeCatalog
        {
            Lookups = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["AppA_1_pub"] = "PRODUCT1",
            },
            Products = new Dictionary<string, StoreProduct>
            {
                ["PRODUCT1"] = MakeProduct("PRODUCT1", "AppA_1_pub", "2.0.0.0", "1.5.0.0"),
            },
        };
        var service = new AppUpdateService(catalog);

        var updates = await service.CheckForUpdatesAsync(new[]
        {
            MakeInstalled("App A", "AppA_1_pub", "1.0.0.0", storeSigned: true),
        });

        var update = Assert.Single(updates);
        Assert.Equal(new Version(2, 0, 0, 0), update.LatestVersion);
        Assert.Equal("App A", update.App.DisplayName);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_SkipsNonStoreAndUnparseableApps()
    {
        var catalog = new FakeCatalog { Lookups = new(), Products = new() };
        var service = new AppUpdateService(catalog);

        var updates = await service.CheckForUpdatesAsync(new[]
        {
            MakeInstalled("Sideloaded", "Sideload_1_pub", "1.0.0.0", storeSigned: false),
            MakeInstalled("NoVersion", "NoVersion_1_pub", null, storeSigned: true),
        });

        Assert.Empty(updates);
        // The catalog must not have been queried for these apps at all.
        Assert.Empty(catalog.LookupCalls);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_IgnoresAppsWithNoCatalogEntry()
    {
        var catalog = new FakeCatalog
        {
            Lookups = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Delisted_1_pub"] = null,
            },
            Products = new(),
        };
        var service = new AppUpdateService(catalog);

        var updates = await service.CheckForUpdatesAsync(new[]
        {
            MakeInstalled("Delisted", "Delisted_1_pub", "1.0.0.0", storeSigned: true),
        });

        Assert.Empty(updates);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_IgnoresSameOrOlderCatalogVersions()
    {
        var catalog = new FakeCatalog
        {
            Lookups = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["AppA_1_pub"] = "PRODUCT1",
            },
            Products = new Dictionary<string, StoreProduct>
            {
                ["PRODUCT1"] = MakeProduct("PRODUCT1", "AppA_1_pub", "1.0.0.0"),
            },
        };
        var service = new AppUpdateService(catalog);

        var updates = await service.CheckForUpdatesAsync(new[]
        {
            MakeInstalled("App A", "AppA_1_pub", "1.0.0.0", storeSigned: true),
        });

        Assert.Empty(updates);
    }

    private static InstalledApp MakeInstalled(string name, string familyName, string? version, bool storeSigned) =>
        new()
        {
            DisplayName = name,
            PackageFullName = $"{familyName.Replace("_1_pub", "_1.0.0.0_x64__pub")}",
            PackageFamilyName = familyName,
            Version = version,
            Publisher = "Test",
            IsNonStore = !storeSigned,
        };

    private static StoreProduct MakeProduct(string productId, string familyName, params string[] versions) =>
        new()
        {
            ProductId = productId,
            Title = "Product",
            PackageFamilyName = familyName,
            Packages = versions
                .Select(version => new CatalogPackage
                {
                    PackageFamilyName = familyName,
                    Version = version,
                })
                .ToArray(),
        };

    private sealed class FakeCatalog : IStoreCatalogClient
    {
        public required Dictionary<string, string?> Lookups { get; init; }

        public required Dictionary<string, StoreProduct> Products { get; init; }

        public List<string> LookupCalls { get; } = new();

        public Task<IReadOnlyList<StoreSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StoreSearchResult>>(Array.Empty<StoreSearchResult>());

        public Task<StoreProduct?> GetProductAsync(string productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Products.TryGetValue(productId, out var product) ? product : null);

        public Task<string?> GetProductIdByPackageFamilyAsync(string packageFamilyName, CancellationToken cancellationToken = default)
        {
            LookupCalls.Add(packageFamilyName);
            return Task.FromResult(
                Lookups.TryGetValue(packageFamilyName, out var productId) ? productId : null);
        }
    }
}
