using StoreCrate.Core.Models;

namespace StoreCrate.Core.Tests;

public class PackageMonikerTests
{
    [Fact]
    public void ParsesStorePackageFullName()
    {
        var moniker = PackageMoniker.Parse("SpotifyAB.SpotifyMusic_1.308.620.0_x64__zpdnekdrzrea0");

        Assert.Equal("SpotifyAB.SpotifyMusic", moniker.Name);
        Assert.Equal("1.308.620.0", moniker.VersionText);
        Assert.Equal("x64", moniker.Architecture);
        Assert.Equal("zpdnekdrzrea0", moniker.PublisherId);
        Assert.Equal(new Version(1, 308, 620, 0), moniker.Version);
    }

    [Fact]
    public void ParsesFrameworkPackage()
    {
        var moniker = PackageMoniker.Parse("Microsoft.VCLibs.140.00_14.0.30704.0_x86__8wekyb3d8bbwe");

        Assert.Equal("Microsoft.VCLibs.140.00", moniker.Name);
        Assert.Equal("14.0.30704.0", moniker.VersionText);
        Assert.Equal("x86", moniker.Architecture);
        Assert.Equal("8wekyb3d8bbwe", moniker.PublisherId);
    }

    [Fact]
    public void HandlesNeutralArchitecture()
    {
        var moniker = PackageMoniker.Parse("Contoso.App_1.0.0.0_neutral__abc123");

        Assert.Equal("Contoso.App", moniker.Name);
        Assert.Equal("neutral", moniker.Architecture);
        Assert.Equal("abc123", moniker.PublisherId);
    }

    [Fact]
    public void HandlesMissingPublisherId()
    {
        var moniker = PackageMoniker.Parse("Contoso.App_1.0.0.0_x64");

        Assert.Equal("Contoso.App", moniker.Name);
        Assert.Equal("1.0.0.0", moniker.VersionText);
        Assert.Equal("x64", moniker.Architecture);
        Assert.Equal(string.Empty, moniker.PublisherId);
    }

    [Fact]
    public void RoundTripsToString()
    {
        const string value = "Contoso.App_1.0.0.0_x64__abc123";

        Assert.Equal(value, PackageMoniker.Parse(value).ToString());
    }

    [Fact]
    public void RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => PackageMoniker.Parse("  "));
    }
}
