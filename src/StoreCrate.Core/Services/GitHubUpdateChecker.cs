using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace StoreCrate.Core.Services;

public sealed class GitHubUpdateChecker : IUpdateChecker
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubUpdateChecker>? _logger;

    public GitHubUpdateChecker(HttpClient httpClient, ILogger<GitHubUpdateChecker>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AppUpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        var uri = new Uri(
            $"https://api.github.com/repos/{AppInfo.RepositoryOwner}/{AppInfo.RepositoryName}/releases/latest");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd($"StoreCrate/{AppInfo.VersionString}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            var versionText = tag.TrimStart('v', 'V');
            if (!Version.TryParse(versionText, out var version))
            {
                return null;
            }

            if (version <= AppInfo.Version)
            {
                return null;
            }

            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    if (name is not null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var urlElement)
                            ? urlElement.GetString()
                            : null;
                        break;
                    }
                }
            }

            return new AppUpdateInfo(
                version,
                tag,
                root.TryGetProperty("html_url", out var htmlUrl) ? htmlUrl.GetString() : null,
                root.TryGetProperty("body", out var body) ? body.GetString() : null,
                downloadUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger?.LogInformation(ex, "Update check failed.");
            return null;
        }
    }
}
