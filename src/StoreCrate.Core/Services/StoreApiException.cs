namespace StoreCrate.Core.Services;

/// <summary>Raised when the Store or delivery services return an unexpected result.</summary>
public sealed class StoreApiException : Exception
{
    public StoreApiException(string message)
        : base(message)
    {
    }

    public StoreApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
