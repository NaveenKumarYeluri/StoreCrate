using StoreCrate.Core.Services;

namespace StoreCrate.Core.Tests;

public class ErrorDetailsTests
{
    [Fact]
    public void JoinsOuterAndInnerMessages()
    {
        var exception = new StoreApiException(
            "Could not download Filelight.",
            new InvalidOperationException("The SSL connection could not be established.", new Exception("SEC_E_WRONG_PRINCIPAL")));

        var message = ErrorDetails.Describe(exception);

        Assert.Contains("Could not download Filelight.", message);
        Assert.Contains("The SSL connection could not be established.", message);
        Assert.Contains("SEC_E_WRONG_PRINCIPAL", message);
    }

    [Fact]
    public void DoesNotRepeatIdenticalMessages()
    {
        var exception = new InvalidOperationException("same", new InvalidOperationException("same"));

        Assert.Equal("same", ErrorDetails.Describe(exception));
    }
}
