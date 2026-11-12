using StoreCrate.Core.Services;

namespace StoreCrate.Core.Tests;

public class StoreJsonParserTests
{
    private const string ProductJson = """
        {
          "Product": {
            "ProductId": "9NCBCSZSJRSB",
            "Market": "US",
            "LocalizedProperties": [
              {
                "Language": "en-US",
                "ProductTitle": "Spotify - Music and Podcasts",
                "PublisherName": "Spotify AB",
                "ShortDescription": "Play millions of songs and podcasts for free.",
                "ProductDescription": "Spotify is a digital music service.",
                "Images": [
                  { "ImagePurpose": "Logo", "Uri": "//store-images.s-microsoft.com/logo.png", "Width": 300, "Height": 300 },
                  { "ImagePurpose": "Tile", "Uri": "https://store-images.s-microsoft.com/tile.png", "Width": 1080, "Height": 1080 },
                  { "ImagePurpose": "Screenshot", "Uri": "https://store-images.s-microsoft.com/shot1.png", "Width": 1920, "Height": 1080 }
                ]
              }
            ],
            "MarketProperties": [
              {
                "Market": "US",
                "OriginalReleaseDate": "2017-06-20T00:00:00Z",
                "UsageData": [
                  { "AggregateTimeSpan": "AllTime", "AverageRating": 4.3, "RatingCount": 1234567 }
                ]
              }
            ],
            "Properties": {
              "PackageFamilyName": "SpotifyAB.SpotifyMusic_zpdnekdrzrea0",
              "PackageIdentityName": "SpotifyAB.SpotifyMusic",
              "PublisherId": "zpdnekdrzrea0",
              "Categories": [ "Music" ]
            },
            "DisplaySkuAvailabilities": [
              {
                "Sku": {
                  "Properties": {
                    "Packages": [
                      {
                        "PackageFullName": "SpotifyAB.SpotifyMusic_1.308.620.0_x64__zpdnekdrzrea0",
                        "PackageFamilyName": "SpotifyAB.SpotifyMusic_zpdnekdrzrea0",
                        "Architectures": [ "x64" ],
                        "MaxDownloadSizeInBytes": 262144000,
                        "MaxInstallSizeInBytes": 524288000,
                        "PackageFormat": "Appx"
                      }
                    ],
                    "FulfillmentData": {
                      "WuCategoryId": "5c353b9c-7ac7-4d27-af07-923e7d9aa2e2"
                    }
                  }
                },
                "Availabilities": [
                  {
                    "OrderManagementData": {
                      "Price": { "ListPrice": 0, "CurrencyCode": "USD" }
                    }
                  }
                ]
              }
            ]
          }
        }
        """;

    private const string SearchJson = """
        [
          {
            "Payload": {
              "PageObject": {
                "CmsData": [
                  {
                    "SearchResults": [
                      {
                        "ProductId": "9NCBCSZSJRSB",
                        "Title": "Spotify - Music and Podcasts",
                        "PublisherName": "Spotify AB",
                        "LongDescription": "Play millions of songs.",
                        "AverageRating": 4.3,
                        "RatingsCount": 1234567,
                        "DisplayPrice": "Free",
                        "Images": [
                          { "ImageType": "Logo", "Url": "//store-images.s-microsoft.com/logo.png" },
                          { "ImageType": "Tile", "Url": "https://store-images.s-microsoft.com/tile.png" }
                        ],
                        "Categories": [ "Music" ],
                        "PackageFamilyNames": [ "SpotifyAB.SpotifyMusic_zpdnekdrzrea0" ]
                      }
                    ]
                  }
                ]
              }
            }
          }
        ]
        """;

    [Fact]
    public void ParsesProductDetails()
    {
        var product = StoreJsonParser.ParseProduct(ProductJson);

        Assert.NotNull(product);
        Assert.Equal("9NCBCSZSJRSB", product!.ProductId);
        Assert.Equal("Spotify - Music and Podcasts", product.Title);
        Assert.Equal("Spotify AB", product.Publisher);
        Assert.Equal("SpotifyAB.SpotifyMusic", product.PackageIdentityName);
        Assert.Equal("SpotifyAB.SpotifyMusic_zpdnekdrzrea0", product.PackageFamilyName);
        Assert.Equal("5c353b9c-7ac7-4d27-af07-923e7d9aa2e2", product.WuCategoryId);
        Assert.Equal("Music", product.PrimaryCategory);
        Assert.Equal("Free", product.PriceText);
        Assert.Equal(4.3, product.AverageRating);
        Assert.Equal(1234567, product.RatingCount);
        Assert.Equal("1.308.620.0", product.VersionText);
    }

    [Fact]
    public void NormalizesProtocolRelativeImageUrls()
    {
        var product = StoreJsonParser.ParseProduct(ProductJson);

        Assert.NotNull(product);
        Assert.Equal("https://store-images.s-microsoft.com/logo.png", product!.LogoUrl);
        Assert.Equal("https://store-images.s-microsoft.com/tile.png", product.HeroImageUrl);
        Assert.Single(product.ScreenshotUrls);
        Assert.True(product.HasScreenshots);
    }

    [Fact]
    public void EstimatesDownloadSizeForArchitecture()
    {
        var product = StoreJsonParser.ParseProduct(ProductJson);

        Assert.NotNull(product);
        Assert.Equal(262144000, product!.EstimateDownloadSizeBytes(System.Runtime.InteropServices.Architecture.X64));

        // With no ARM64 package advertised, the estimate falls back to every known package.
        Assert.Equal(262144000, product.EstimateDownloadSizeBytes(System.Runtime.InteropServices.Architecture.Arm64));
    }

    [Fact]
    public void ParsesNestedSearchResults()
    {
        var results = StoreJsonParser.ParseSearch(SearchJson);

        var result = Assert.Single(results);
        Assert.Equal("9NCBCSZSJRSB", result.ProductId);
        Assert.Equal("Spotify - Music and Podcasts", result.Title);
        Assert.Equal("https://store-images.s-microsoft.com/logo.png", result.LogoUrl);
        Assert.Equal("Free", result.PriceText);
        Assert.Equal("SpotifyAB.SpotifyMusic_zpdnekdrzrea0", Assert.Single(result.PackageFamilyNames));
    }

    [Fact]
    public void ReturnsEmptySearchWhenResultsAreMissing()
    {
        Assert.Empty(StoreJsonParser.ParseSearch("""{"Payload":{}}"""));
    }

    [Fact]
    public void ReturnsNullProductForUnexpectedShape()
    {
        Assert.Null(StoreJsonParser.ParseProduct("""{"ProductId":"123"}"""));
        Assert.Null(StoreJsonParser.ParseProduct("""{"Product":{}}"""));
    }

    [Fact]
    public void FallsBackToShortDescriptionWhenFullDescriptionMissing()
    {
        const string json = """
            {
              "Product": {
                "ProductId": "X",
                "LocalizedProperties": [
                  { "ProductTitle": "Example", "ShortDescription": "Short text" }
                ]
              }
            }
            """;

        var product = StoreJsonParser.ParseProduct(json);

        Assert.NotNull(product);
        Assert.Equal("Short text", product!.SummaryText);
        Assert.Equal("Short text", product.Description);
    }
}
