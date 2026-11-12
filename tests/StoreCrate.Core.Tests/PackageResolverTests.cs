using System.Runtime.InteropServices;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.Core.Tests;

public class PackageResolverTests
{
    private static readonly StoreProduct Product = new()
    {
        ProductId = "9NCBCSZSJRSB",
        Title = "Spotify",
        PackageFamilyName = "SpotifyAB.SpotifyMusic_zpdnekdrzrea0",
        PackageIdentityName = "SpotifyAB.SpotifyMusic",
    };

    private static Fe3Package Package(string moniker, string? type = "Appx") => new()
    {
        UpdateId = Guid.NewGuid().ToString(),
        Revision = "1",
        Moniker = moniker,
        PackageType = type,
        Url = "https://example.invalid/package.appx",
    };

    [Fact]
    public void PicksHostArchitectureForMainAndDependencies()
    {
        var packages = new[]
        {
            Package("SpotifyAB.SpotifyMusic_1.300.0.0_x64__zpdnekdrzrea0"),
            Package("SpotifyAB.SpotifyMusic_1.300.0.0_x86__zpdnekdrzrea0"),
            Package("Microsoft.VCLibs.140.00_14.0.30704.0_x64__8wekyb3d8bbwe"),
            Package("Microsoft.VCLibs.140.00_14.0.30704.0_arm64__8wekyb3d8bbwe"),
        };

        var plan = new PackageResolver().Resolve(Product, packages, Architecture.X64);

        Assert.Equal("x64", plan.MainPackage.Architecture);
        Assert.Single(plan.Dependencies);
        Assert.Equal("x64", plan.Dependencies[0].Architecture);
        Assert.Equal(2, plan.AllPackages.Count);
        Assert.Same(plan.MainPackage, plan.AllPackages[^1]);
    }

    [Fact]
    public void PrefersNewestVersionAmongEqualArchitectures()
    {
        var packages = new[]
        {
            Package("SpotifyAB.SpotifyMusic_1.100.0.0_x64__zpdnekdrzrea0"),
            Package("SpotifyAB.SpotifyMusic_1.300.0.0_x64__zpdnekdrzrea0"),
        };

        var plan = new PackageResolver().Resolve(Product, packages, Architecture.X64);

        Assert.Equal(new Version(1, 300, 0, 0), plan.MainPackage.Version);
    }

    [Fact]
    public void FallsBackToNeutralPackages()
    {
        var packages = new[]
        {
            Package("Contoso.App_1.0.0.0_neutral__abc123"),
        };

        var product = Product with
        {
            PackageIdentityName = "Contoso.App",
            PackageFamilyName = "Contoso.App_abc123",
        };

        var plan = new PackageResolver().Resolve(product, packages, Architecture.Arm64);

        Assert.Equal("neutral", plan.MainPackage.Architecture);
    }

    [Fact]
    public void ExcludesXapPackages()
    {
        var packages = new[]
        {
            Package("Contoso.App_1.0.0.0_x64__abc123", "XAP"),
            Package("Contoso.App_2.0.0.0_x64__abc123"),
        };

        var product = Product with
        {
            PackageIdentityName = "Contoso.App",
            PackageFamilyName = "Contoso.App_abc123",
        };

        var plan = new PackageResolver().Resolve(product, packages, Architecture.X64);

        Assert.Equal(new Version(2, 0, 0, 0), plan.MainPackage.Version);
    }

    [Fact]
    public void ThrowsWhenNoCompatiblePackageExists()
    {
        var packages = new[]
        {
            Package("SpotifyAB.SpotifyMusic_1.300.0.0_x64__zpdnekdrzrea0"),
        };

        Assert.Throws<StoreApiException>(() =>
            new PackageResolver().Resolve(Product, packages, Architecture.Arm));
    }

    [Fact]
    public void ThrowsWhenPackageListIsEmpty()
    {
        Assert.Throws<StoreApiException>(() =>
            new PackageResolver().Resolve(Product, Array.Empty<Fe3Package>(), Architecture.X64));
    }

    [Theory]
    [InlineData("x64", Architecture.X64, 100)]
    [InlineData("neutral", Architecture.X64, 60)]
    [InlineData("x86", Architecture.X64, 20)]
    [InlineData("arm64", Architecture.X64, -1)]
    [InlineData("arm64", Architecture.Arm64, 100)]
    [InlineData("x64", Architecture.Arm64, 20)]
    [InlineData("x86", Architecture.X86, 100)]
    [InlineData("neutral", Architecture.X86, 60)]
    [InlineData("x64", Architecture.X86, -1)]
    public void ScoresArchitectures(string architecture, Architecture host, int expected)
    {
        Assert.Equal(expected, PackageResolver.ArchitectureScore(architecture, host));
    }
}
