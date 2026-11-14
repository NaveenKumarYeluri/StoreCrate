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
    public void ParsesTildeResourceIdMoniker()
    {
        var moniker = PackageMoniker.Parse("55746MarkSmirnov.BluetoothAudioReveicer_1.1.5.70_neutral_~_xwrbx6997tsfc");

        Assert.Equal("55746MarkSmirnov.BluetoothAudioReveicer", moniker.Name);
        Assert.Equal("1.1.5.70", moniker.VersionText);
        Assert.Equal("neutral", moniker.Architecture);
        Assert.Equal("xwrbx6997tsfc", moniker.PublisherId);
        Assert.Equal(new Version(1, 1, 5, 70), moniker.Version);
    }

    [Fact]
    public void ParsesNamedResourceIdMoniker()
    {
        var moniker = PackageMoniker.Parse("Contoso.App_1.0.0.0_x64_scale-100_abc123");

        Assert.Equal("Contoso.App", moniker.Name);
        Assert.Equal("1.0.0.0", moniker.VersionText);
        Assert.Equal("x64", moniker.Architecture);
        Assert.Equal("abc123", moniker.PublisherId);
    }

    [Fact]
    public void ParsesPackageFamilyName()
    {
        var moniker = PackageMoniker.Parse("55746MarkSmirnov.BluetoothAudioReveicer_xwrbx6997tsfc");

        Assert.Equal("55746MarkSmirnov.BluetoothAudioReveicer", moniker.Name);
        Assert.Equal(string.Empty, moniker.VersionText);
        Assert.Equal("neutral", moniker.Architecture);
        Assert.Equal("xwrbx6997tsfc", moniker.PublisherId);
    }

    [Fact]
    public void RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => PackageMoniker.Parse("  "));
    }
}
