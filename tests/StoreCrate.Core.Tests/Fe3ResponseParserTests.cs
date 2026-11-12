using StoreCrate.Core.Services;

namespace StoreCrate.Core.Tests;

public class Fe3ResponseParserTests
{
    [Fact]
    public void ParsesUpdateIdentitiesAndMetadata()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <SyncUpdatesResponse xmlns="http://schemas.microsoft.com/wsus/2005/04/CorporatePublishing/SyncUpdatesResponse">
              <UpdateInfo>
                <UpdateIdentity UpdateID="11111111-1111-1111-1111-111111111111" RevisionNumber="1" />
                <AppxMetadata PackageMoniker="SpotifyAB.SpotifyMusic_1.308.620.0_x64__zpdnekdrzrea0"
                             PackageType="Appx"
                             ContentType="Package" />
              </UpdateInfo>
              <UpdateInfo>
                <UpdateIdentity UpdateID="22222222-2222-2222-2222-222222222222" RevisionNumber="2" />
                <AppxMetadata PackageMoniker="Microsoft.VCLibs.140.00_14.0.30704.0_x64__8wekyb3d8bbwe"
                             PackageType="Appx"
                             ContentType="Package" />
              </UpdateInfo>
            </SyncUpdatesResponse>
            """;

        var entries = Fe3ResponseParser.Parse(xml);

        Assert.Equal(2, entries.Count);
        Assert.Equal("11111111-1111-1111-1111-111111111111", entries[0].UpdateId);
        Assert.Equal("1", entries[0].Revision);
        Assert.Equal("SpotifyAB.SpotifyMusic_1.308.620.0_x64__zpdnekdrzrea0", entries[0].Moniker);
        Assert.Equal("Appx", entries[0].PackageType);
        Assert.Equal("Microsoft.VCLibs.140.00_14.0.30704.0_x64__8wekyb3d8bbwe", entries[1].Moniker);
    }

    [Fact]
    public void SkipsDuplicateMonikers()
    {
        const string xml = """
            <SyncUpdatesResponse>
              <UpdateInfo>
                <UpdateIdentity UpdateID="11111111-1111-1111-1111-111111111111" RevisionNumber="1" />
                <AppxMetadata PackageMoniker="Contoso.App_1.0.0.0_x64__abc123" PackageType="Appx" />
              </UpdateInfo>
              <UpdateInfo>
                <UpdateIdentity UpdateID="22222222-2222-2222-2222-222222222222" RevisionNumber="2" />
                <AppxMetadata PackageMoniker="Contoso.App_1.0.0.0_x64__abc123" PackageType="Appx" />
              </UpdateInfo>
            </SyncUpdatesResponse>
            """;

        var entries = Fe3ResponseParser.Parse(xml);

        Assert.Single(entries);
    }

    [Fact]
    public void SkipsIncompleteEntries()
    {
        const string xml = """
            <SyncUpdatesResponse>
              <UpdateInfo>
                <UpdateIdentity UpdateID="11111111-1111-1111-1111-111111111111" RevisionNumber="1" />
              </UpdateInfo>
              <UpdateInfo>
                <AppxMetadata PackageMoniker="Contoso.App_1.0.0.0_x64__abc123" PackageType="Appx" />
              </UpdateInfo>
            </SyncUpdatesResponse>
            """;

        Assert.Empty(Fe3ResponseParser.Parse(xml));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReturnsEmptyForMissingXml(string? xml)
    {
        Assert.Empty(Fe3ResponseParser.Parse(xml));
    }
}
