using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using StoreCrate.Core.Logging;
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

        // SEC-1: Compute SHA-256 of the downloaded zip for audit purposes.
        // TODO SEC-1: verify SHA-256 against a published checksum once release workflow publishes one.
        using (var sha256 = SHA256.Create())
        {
            await using var zipStream = File.OpenRead(zipPath);
            var hashBytes = await sha256.ComputeHashAsync(zipStream, cancellationToken).ConfigureAwait(false);
            var hashHex = Convert.ToHexString(hashBytes);
            AppLog.Info($"[SelfUpdate] Downloaded update.zip SHA-256: {hashHex}");
        }

        var extracted = Path.Combine(root, "new");

        // SEC-2: Manual extraction with path traversal (Zip Slip) protection.
        using (var archive = ZipFile.OpenRead(zipPath))
        {
            var extractedFull = Path.GetFullPath(extracted) + Path.DirectorySeparatorChar;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                // Skip directory entries
                if (entry.FullName.EndsWith('/'))
                    continue;

                var dest = Path.GetFullPath(Path.Combine(extracted, entry.FullName));
                if (!dest.StartsWith(extractedFull, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Zip entry '{entry.FullName}' would extract outside the target directory.");

                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
            }
        }

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

        // SEC-3: Escape target and source paths before embedding in batch script to prevent command injection.
        var safeTarget = EscapeBatchVar(target.TrimEnd('\\'));
        var safeSource = EscapeBatchVar(source.TrimEnd('\\'));

        // The copy loop retries while StoreCrate.exe is still locked by the running
        // instance, so there is no need to watch the process id.
        var lines = new[]
        {
            "@echo off",
            "setlocal",
            $"set \"TARGET={safeTarget}\"",
            $"set \"SOURCE={safeSource}\"",
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

    /// <summary>
    /// Escapes a value for safe embedding in a batch file variable assignment.
    /// Throws if the value contains newline characters that could break out of the assignment.
    /// </summary>
    private static string EscapeBatchVar(string value)
    {
        if (value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            throw new InvalidOperationException("Update path contains illegal characters.");
        return value.Replace("\"", "\\\"");
    }

    private static string ExeName => "StoreCrate.exe";
}
