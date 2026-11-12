using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Media.Imaging;
using StoreCrate.Core.Services;

namespace StoreCrate.App.Services;

/// <summary>Loads and caches product artwork from the Store image CDN.</summary>
public sealed class ImageCache
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _tasks = new(StringComparer.Ordinal);
    private readonly string _cacheDirectory;

    public ImageCache(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _cacheDirectory = Path.Combine(AppPaths.CacheDirectory, "images");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public Task<Bitmap?> GetAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult<Bitmap?>(null);
        }

        return _tasks.GetOrAdd(url, LoadAsync);
    }

    private async Task<Bitmap?> LoadAsync(string url)
    {
        try
        {
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
            var path = Path.Combine(_cacheDirectory, key + ".img");

            if (!File.Exists(path))
            {
                var bytes = await _httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
                var temp = path + ".tmp";
                await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(false);
                File.Move(temp, path, overwrite: true);
            }

            await using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
