using StoreCrate.Core.Services;

namespace StoreCrate.Core.Tests;

public class Fe3ClientTests
{
    private const string SyncUpdatesXml = """
        <SyncUpdatesResponse xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
          <SyncUpdatesResult>
            <NewUpdates>
              <UpdateInfo>
                <UpdateIdentity UpdateID="11111111-1111-1111-1111-111111111111" RevisionNumber="1" />
                <Xml>
                  <Metadata>
                    <AppxPackageMetadata>
                      <AppxMetadata PackageType="UAP" PackageMoniker="KDEe.V.Filelight_25.800.1836.0_x64__7vt06qxq7ptv8" />
                    </AppxPackageMetadata>
                  </Metadata>
                </Xml>
              </UpdateInfo>
            </NewUpdates>
          </SyncUpdatesResult>
        </SyncUpdatesResponse>
        """;

    [Fact]
    public async Task KeepsPlainHttpDeliveryUrlReturnedByFe3()
    {
        var client = new Fe3Client(new FakeTransport(
            new Uri("http://tlu.dl.delivery.mp.microsoft.com/filestreamingservice/files/abc")));

        var packages = await client.GetPackagesAsync("05d83631-edfe-4d26-b4be-b14341c9f213");

        var package = Assert.Single(packages);
        Assert.Equal(
            "http://tlu.dl.delivery.mp.microsoft.com/filestreamingservice/files/abc",
            package.Url);
    }

    [Fact]
    public async Task KeepsHttpsDeliveryUrlReturnedByFe3()
    {
        var client = new Fe3Client(new FakeTransport(
            new Uri("https://example.invalid/filestreamingservice/files/abc")));

        var packages = await client.GetPackagesAsync("05d83631-edfe-4d26-b4be-b14341c9f213");

        var package = Assert.Single(packages);
        Assert.Equal("https://example.invalid/filestreamingservice/files/abc", package.Url);
    }

    private sealed class FakeTransport(params Uri[] urls) : IFe3Transport
    {
        public Task<string> SyncUpdatesAsync(string wuCategoryId) => Task.FromResult(SyncUpdatesXml);

        public Task<IList<Uri>> GetFileUrlsAsync(string[] updateIds, string[] revisions) =>
            Task.FromResult<IList<Uri>>(urls);
    }
}
