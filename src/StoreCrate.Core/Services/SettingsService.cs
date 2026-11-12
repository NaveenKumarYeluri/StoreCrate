using System.Text.Json;
using Microsoft.Extensions.Logging;
using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _settingsPath;
    private readonly ILogger<SettingsService>? _logger;

    public SettingsService(string? settingsPath = null, ILogger<SettingsService>? logger = null)
    {
        _settingsPath = settingsPath ?? AppPaths.SettingsFile;
        _logger = logger;
    }

    public AppSettings Current { get; private set; } = new();

    public event EventHandler? Changed;

    public string DownloadDirectory =>
        string.IsNullOrWhiteSpace(Current.DownloadDirectory)
            ? AppPaths.DefaultDownloadDirectory
            : Environment.ExpandEnvironmentVariables(Current.DownloadDirectory);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
                Current = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Could not read settings from {Path}; using defaults.", _settingsPath);
            Current = new AppSettings();
        }

        Normalize(Current);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(Current, SerializerOptions);
            var tempPath = _settingsPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, _settingsPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Could not save settings to {Path}.", _settingsPath);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void Normalize(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Market))
        {
            settings.Market = "US";
        }

        if (string.IsNullOrWhiteSpace(settings.Locale))
        {
            settings.Locale = "en-US";
        }

        if (settings.MaxConcurrentDownloads is < 1 or > 8)
        {
            settings.MaxConcurrentDownloads = 2;
        }

        if (settings.Theme is not ("System" or "Light" or "Dark"))
        {
            settings.Theme = "System";
        }
    }
}
