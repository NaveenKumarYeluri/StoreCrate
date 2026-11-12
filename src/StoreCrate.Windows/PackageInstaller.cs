using System.Text;
using Microsoft.Extensions.Logging;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;
using Windows.Management.Deployment;

namespace StoreCrate.Windows;

/// <summary>
/// Installs packages with the Windows <see cref="PackageManager"/> API and falls back to
/// <c>Add-AppxPackage</c> when the WinRT deployment path is unavailable.
/// </summary>
public sealed class PackageInstaller : IAppInstaller
{
    private readonly ILogger<PackageInstaller>? _logger;

    public PackageInstaller(ILogger<PackageInstaller>? logger = null)
    {
        _logger = logger;
    }

    public async Task<InstallResult> InstallAsync(
        InstallPlan plan,
        IReadOnlyList<DownloadedPackage> packages,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(packages);

        var paths = packages.ToDictionary(
            p => p.Package.Moniker,
            p => p.FilePath,
            StringComparer.OrdinalIgnoreCase);

        if (!paths.TryGetValue(plan.MainPackage.Moniker, out var mainPath) || !File.Exists(mainPath))
        {
            return InstallResult.Failed("The main package file is missing. Please retry the download.");
        }

        var dependencies = new List<(Fe3Package Package, string Path)>();
        foreach (var dependency in plan.Dependencies)
        {
            if (paths.TryGetValue(dependency.Moniker, out var path) && File.Exists(path))
            {
                dependencies.Add((dependency, path));
            }
        }

        try
        {
            var manager = new PackageManager();
            var pending = FilterSatisfiedDependencies(manager, dependencies);
            var dependencyPaths = pending.Select(d => d.Path).ToList();

            var result = await InstallWithPackageManagerAsync(
                    manager,
                    mainPath,
                    dependencyPaths,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                return result;
            }

            _logger?.LogWarning("PackageManager install failed ({Code}): {Message}", result.ErrorCode, result.Message);

            var fallback = await InstallWithPowerShellAsync(mainPath, dependencyPaths, cancellationToken)
                .ConfigureAwait(false);

            return fallback.IsSuccess
                ? fallback
                : result with { Message = $"{result.Message} {fallback.Message}".Trim() };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Install failed for {Title}.", plan.Product.Title);
            return MapException(ex);
        }
    }

    private static async Task<InstallResult> InstallWithPackageManagerAsync(
        PackageManager manager,
        string mainPath,
        IReadOnlyList<string> dependencies,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var options = new AddPackageOptions { ForceTargetAppShutdown = true };
        foreach (var dependency in dependencies)
        {
            options.DependencyPackageUris.Add(new Uri(Path.GetFullPath(dependency)));
        }

        var operation = manager.AddPackageByUriAsync(new Uri(Path.GetFullPath(mainPath)), options);
        operation.Progress = (_, value) =>
            progress?.Report(Math.Clamp(value.percentage / 100d, 0, 1));

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                operation.Cancel();
            }
            catch
            {
                // Best effort.
            }
        });

        var result = await operation;
        cancellationToken.ThrowIfCancellationRequested();

        if (result.ExtendedErrorCode is null)
        {
            progress?.Report(1);
            return InstallResult.Success();
        }

        return MapError(result.ExtendedErrorCode, result.ErrorText);
    }

    private static List<(Fe3Package Package, string Path)> FilterSatisfiedDependencies(
        PackageManager manager,
        List<(Fe3Package Package, string Path)> dependencies)
    {
        if (dependencies.Count == 0)
        {
            return dependencies;
        }

        var installed = new Dictionary<string, Version>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var package in manager.FindPackagesForUser(string.Empty))
            {
                var version = package.Id.Version;
                installed[package.Id.Name] = new Version(
                    version.Major,
                    version.Minor,
                    version.Build,
                    version.Revision);
            }
        }
        catch
        {
            return dependencies;
        }

        return dependencies
            .Where(dependency =>
                dependency.Package.Version is not { } required ||
                !installed.TryGetValue(dependency.Package.Name, out var current) ||
                current < required)
            .ToList();
    }

    private static async Task<InstallResult> InstallWithPowerShellAsync(
        string mainPath,
        IReadOnlyList<string> dependencies,
        CancellationToken cancellationToken)
    {
        var script = new StringBuilder();
        script.AppendLine("$ErrorActionPreference = 'Stop'");
        script.Append("Add-AppxPackage -Path '").Append(Escape(mainPath)).Append('\'');

        if (dependencies.Count > 0)
        {
            script.Append(" -DependencyPath ");
            script.Append(string.Join(",", dependencies.Select(d => $"'{Escape(d)}'")));
        }

        script.AppendLine(" -ForceApplicationShutdown");
        script.AppendLine("exit 0");

        PowerShellResult result;
        try
        {
            result = await PowerShellRunner
                .RunAsync(script.ToString(), TimeSpan.FromMinutes(20), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return InstallResult.Failed($"Could not run the package installer: {ex.Message}");
        }

        if (result.Succeeded)
        {
            return InstallResult.Success();
        }

        return MapPowerShellError(result.Combined);
    }

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static InstallResult MapException(Exception exception) =>
        InstallResult.Failed($"Installation failed: {exception.Message}");

    private static InstallResult MapError(Exception error, string? text)
    {
        var code = $"0x{error.HResult:X8}";
        return error.HResult switch
        {
            unchecked((int)0x80073CF3) => InstallResult.NeedsAttention(
                "A required dependency is missing or incompatible with this device.", code),
            unchecked((int)0x803F8001) => InstallResult.NeedsAttention(
                "This app requires a Microsoft Store license or purchase. StoreCrate can only install free or owned apps.", code),
            unchecked((int)0x80073D02) => InstallResult.NeedsAttention(
                "The app is currently running. Close it and try again.", code),
            unchecked((int)0x80073CFB) => InstallResult.NeedsAttention(
                "Another version of this app is already installed and conflicts with this package.", code),
            unchecked((int)0x80073CF9) => InstallResult.NeedsAttention(
                "Windows could not register the package. Check that the package is valid and signed by Microsoft.", code),
            _ => InstallResult.Failed(
                string.IsNullOrWhiteSpace(text) ? $"Installation failed ({code})." : $"{text} ({code})",
                code),
        };
    }

    private static InstallResult MapPowerShellError(string output)
    {
        if (output.Contains("0x803F8001", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("license", StringComparison.OrdinalIgnoreCase))
        {
            return InstallResult.NeedsAttention(
                "This app requires a Microsoft Store license or purchase. StoreCrate can only install free or owned apps.",
                "0x803F8001");
        }

        if (output.Contains("0x80073CF3", StringComparison.OrdinalIgnoreCase))
        {
            return InstallResult.NeedsAttention(
                "A required dependency is missing or incompatible with this device.",
                "0x80073CF3");
        }

        var message = output.Length > 900 ? output[^900..] : output;
        return InstallResult.Failed(
            string.IsNullOrWhiteSpace(message) ? "Installation failed." : message.Trim());
    }
}
