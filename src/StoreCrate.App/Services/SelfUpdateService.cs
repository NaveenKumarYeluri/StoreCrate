using System.Diagnostics;
using System.IO.Compression;
using StoreCrate.Core.Services;

namespace StoreCrate.App.Services;

/// <summary>
/// Downloads a newer StoreCrate release from GitHub and prepares a small batch script
/// that swaps the application files after the running instance exits, then restarts it.
/// </summary>
public sealed class SelfUpdateService
{
    private readonly HttpClient _httpClient;

    public SelfUpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>The directory the update is staged in.</summary>
    public string StagingDirectory { get; private set; } = string.Empty;

    /// <summary>Downloads and extracts the release zip so it is ready to apply.</summary>
    public async Task DownloadAndStageAsync(AppUpdateInfo update, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            throw new InvalidOperationException("The release does not contain a downloadable package.");
        }

        var root = Path.Combine(Path.GetTempPath(), "StoreCrate-update");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.CreateDirectory(root);
        StagingDirectory = root;

        var zipPath = Path.Combine(root, "update.zip");
        await using (var stream = await _httpClient
            .GetStreamAsync(update.DownloadUrl, cancellationToken)
            .ConfigureAwait(false))
        {
            await using var file = File.Create(zipPath);
            await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        var extracted = Path.Combine(root, "new");
        ZipFile.ExtractToDirectory(zipPath, extracted, overwriteFiles: true);

        // Some archives wrap everything in a single top-level folder.
        if (!Directory.EnumerateFiles(extracted, "*.exe", SearchOption.AllDirectories).Any())
        {
            var children = Directory.EnumerateDirectories(extracted).ToList();
            if (children.Count == 1)
            {
                extracted = children[0];
            }
        }

        if (!File.Exists(Path.Combine(extracted, ExeName)))
        {
            throw new InvalidOperationException("The downloaded update does not contain the StoreCrate executable.");
        }

        ExtractedDirectory = extracted;
    }

    public string ExtractedDirectory { get; private set; } = string.Empty;

    /// <summary>
    /// Starts the helper script that retries copying the staged files until the running
    /// instance releases them, then relaunches StoreCrate.
    /// </summary>
    public void ApplyOnExit()
    {
        var source = ExtractedDirectory;
        var target = AppContext.BaseDirectory;
        var script = Path.Combine(StagingDirectory, "update-storecrate.bat");
        var exeName = Path.GetFileName(Environment.ProcessPath) is { Length: > 0 } name ? name : ExeName;

        // The copy loop retries while StoreCrate.exe is still locked by the running
        // instance, so there is no need to watch the process id.
        var lines = new[]
        {
            "@echo off",
            "setlocal",
            $"set \"TARGET={target.TrimEnd('\\')}\"",
            $"set \"SOURCE={source.TrimEnd('\\')}\"",
            "ping -n 3 127.0.0.1 >nul",
            ":copyloop",
            "xcopy /Y /E /I \"%SOURCE%\\*\" \"%TARGET%\\\" >nul 2>&1",
            "if errorlevel 1 (",
            "  ping -n 2 127.0.0.1 >nul",
            "  goto copyloop",
            ")",
            $"start \"\" \"%TARGET%\\{exeName}\"",
            "rd /S /Q \"%TEMP%\\StoreCrate-update\"",
        };

        File.WriteAllText(script, string.Join(Environment.NewLine, lines));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            },
        };
        process.Start();
    }

    private static string ExeName => "StoreCrate.exe";
}
