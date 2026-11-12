using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

public sealed class DownloadService : IDownloadService
{
    private const int BufferSize = 81920;

    private readonly HttpClient _httpClient;
    private readonly ILogger<DownloadService>? _logger;

    public DownloadService(HttpClient httpClient, ILogger<DownloadService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DownloadedPackage> DownloadAsync(
        Fe3Package package,
        string destinationDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        Directory.CreateDirectory(destinationDirectory);

        var (fileName, probedTotal) = await ProbeAsync(package, cancellationToken).ConfigureAwait(false);
        var finalPath = Path.Combine(destinationDirectory, fileName);
        var partialPath = finalPath + ".part";

        if (File.Exists(finalPath))
        {
            var existingLength = new FileInfo(finalPath).Length;
            if (probedTotal is null || existingLength == probedTotal)
            {
                return new DownloadedPackage(package, finalPath, existingLength);
            }

            File.Delete(finalPath);
        }

        var existing = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;
        if (probedTotal is not null && existing > probedTotal)
        {
            File.Delete(partialPath);
            existing = 0;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, package.Url);
        request.Headers.UserAgent.ParseAdd($"StoreCrate/{AppInfo.VersionString}");
        if (existing > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existing, null);
        }

        using var response = await SendAsync(request, package, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && existing > 0)
        {
            // The partial file is already the complete package.
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(partialPath, finalPath);
            return new DownloadedPackage(package, finalPath, existing);
        }

        response.EnsureSuccessStatusCode();

        if (existing > 0 && response.StatusCode == HttpStatusCode.OK)
        {
            // Server ignored the range request; restart from scratch.
            existing = 0;
        }

        var total = probedTotal;
        if (total is null && response.Content.Headers.ContentLength is { } contentLength)
        {
            total = contentLength + existing;
        }

        var received = existing;
        var lastReport = DateTime.UtcNow;

        await using (var destination = new FileStream(
                         partialPath,
                         existing > 0 ? FileMode.Append : FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         BufferSize,
                         useAsync: true))
        {
            await using var source = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            var buffer = new byte[BufferSize];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                received += read;

                var now = DateTime.UtcNow;
                if (now - lastReport >= TimeSpan.FromMilliseconds(150))
                {
                    progress?.Report(new DownloadProgress(package.Moniker, fileName, received, total));
                    lastReport = now;
                }
            }
        }

        progress?.Report(new DownloadProgress(package.Moniker, fileName, received, total));

        if (total is not null && received != total)
        {
            throw new StoreApiException(
                $"Download of {fileName} was incomplete ({received} of {total} bytes). It will resume next time.");
        }

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }

        File.Move(partialPath, finalPath);
        _logger?.LogInformation("Downloaded {FileName} ({Bytes} bytes).", fileName, received);

        return new DownloadedPackage(package, finalPath, received);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        Fe3Package package,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new StoreApiException($"Could not download {package.Moniker}.", ex);
        }
    }

    private async Task<(string FileName, long? TotalBytes)> ProbeAsync(
        Fe3Package package,
        CancellationToken cancellationToken)
    {
        var fallbackName = SanitizeFileName(package.Moniker) + GuessExtension(package);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, package.Url);
            request.Headers.UserAgent.ParseAdd($"StoreCrate/{AppInfo.VersionString}");
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var name = ExtractFileName(response.Content.Headers) ?? fallbackName;
                return (name, response.Content.Headers.ContentLength);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger?.LogDebug(ex, "HEAD probe failed for {Moniker}.", package.Moniker);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, package.Url);
            request.Headers.Range = new RangeHeaderValue(0, 0);
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var name = ExtractFileName(response.Content.Headers) ?? fallbackName;
                var total = response.Content.Headers.ContentRange?.Length;
                return (name, total);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger?.LogDebug(ex, "Range probe failed for {Moniker}.", package.Moniker);
        }

        return (fallbackName, null);
    }

    private static string? ExtractFileName(HttpContentHeaders headers)
    {
        var name = headers.ContentDisposition?.FileNameStar ?? headers.ContentDisposition?.FileName;
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return SanitizeFileName(name.Trim().Trim('"'));
    }

    private static string GuessExtension(Fe3Package package) =>
        package.Moniker.Contains("bundle", StringComparison.OrdinalIgnoreCase) ? ".msixbundle" : ".appx";

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(cleaned) ? "package.appx" : cleaned;
    }
}
