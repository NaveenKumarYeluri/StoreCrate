using System.Diagnostics;
using System.Text;

namespace StoreCrate.Windows;

internal sealed record PowerShellResult(int ExitCode, string Output, string Error)
{
    public bool Succeeded => ExitCode == 0;

    public string Combined =>
        string.Join(
            Environment.NewLine,
            new[] { Output, Error }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>Runs a PowerShell script via <c>-EncodedCommand</c> to avoid quoting issues.</summary>
internal static class PowerShellRunner
{
    public static async Task<PowerShellResult> RunAsync(
        string script,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encoded);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start PowerShell.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout is not null)
        {
            linked.CancelAfter(timeout.Value);
        }

        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort.
            }

            throw;
        }

        return new PowerShellResult(process.ExitCode, await outputTask, await errorTask);
    }
}
