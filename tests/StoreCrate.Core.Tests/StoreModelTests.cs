using StoreCrate.Core.Models;

namespace StoreCrate.Core.Tests;

public class StoreModelTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(262144000, "250 MB")]
    [InlineData(1073741824, "1 GB")]
    public void FormatsByteSizes(long bytes, string expected)
    {
        Assert.Equal(expected, StoreProduct.FormatBytes(bytes));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("https://example.com/a.png", "https://example.com/a.png")]
    [InlineData("//example.com/a.png", "https://example.com/a.png")]
    public void NormalizesImageUrls(string? url, string expected)
    {
        Assert.Equal(expected, StoreImage.NormalizeUrl(url));
    }

    [Fact]
    public void ExposesFriendlyFallbacks()
    {
        var product = new StoreProduct
        {
            ProductId = "X",
            Title = "Example",
        };

        Assert.Equal("Unknown publisher", product.PublisherText);
        Assert.Equal("Free", product.PriceText);
        Assert.Equal("Unknown", product.VersionText);
        Assert.Equal("Not rated", product.RatingText);
        Assert.Equal("No ratings yet", product.RatingCountText);
        Assert.Equal("Unknown", product.ReleaseDateText);
        Assert.Equal("Any", product.ArchitectureText);
        Assert.Equal("Unknown size", product.SizeText);
        Assert.False(product.HasScreenshots);
    }

    [Fact]
    public void InstallPlanOrdersDependenciesBeforeMainPackage()
    {
        static Fe3Package Package(string name) => new()
        {
            UpdateId = name,
            Revision = "1",
            Moniker = $"{name}_1.0.0.0_x64__abc123",
            Url = "https://example.invalid",
        };

        var plan = new InstallPlan
        {
            Product = new StoreProduct { ProductId = "X", Title = "Example" },
            MainPackage = Package("Contoso.App"),
            Dependencies = [Package("Microsoft.VCLibs.140.00")],
        };

        Assert.Equal(2, plan.AllPackages.Count);
        Assert.Equal("Microsoft.VCLibs.140.00", plan.AllPackages[0].Name);
        Assert.Equal("Contoso.App", plan.AllPackages[1].Name);
    }

    [Fact]
    public void InstalledAppExposesInitialAndFallbacks()
    {
        var app = new InstalledApp
        {
            DisplayName = "spotify",
            PackageFullName = "SpotifyAB.SpotifyMusic_1.308.620.0_x64__zpdnekdrzrea0",
            PackageFamilyName = "SpotifyAB.SpotifyMusic_zpdnekdrzrea0",
        };

        Assert.Equal("S", app.Initial);
        Assert.Equal("Unknown publisher", app.PublisherText);
        Assert.Equal("Unknown", app.VersionText);
    }
}
