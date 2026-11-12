namespace StoreCrate.Core.Models;

/// <summary>Progress reported while downloading a package.</summary>
public sealed record DownloadProgress(
    string Moniker,
    string FileName,
    long BytesReceived,
    long? TotalBytes)
{
    public double Fraction =>
        TotalBytes is > 0 ? Math.Clamp((double)BytesReceived / TotalBytes.Value, 0, 1) : 0;

    public int Percent => (int)Math.Round(Fraction * 100);

    public string SizeText =>
        TotalBytes is > 0
            ? $"{StoreProduct.FormatBytes(BytesReceived)} of {StoreProduct.FormatBytes(TotalBytes.Value)}"
            : StoreProduct.FormatBytes(BytesReceived);
}

/// <summary>The result of a completed download.</summary>
public sealed record DownloadedPackage(Fe3Package Package, string FilePath, long SizeBytes);
