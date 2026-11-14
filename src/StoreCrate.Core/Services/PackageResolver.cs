using System.Runtime.InteropServices;
using StoreCrate.Core.Models;

namespace StoreCrate.Core.Services;

public sealed class PackageResolver : IPackageResolver
{
    private static readonly string[] FrameworkPrefixes =
    [
        "Microsoft.VCLibs",
        "Microsoft.NET.Native",
        "Microsoft.UI.Xaml",
        "Microsoft.WindowsAppRuntime",
        "Microsoft.Services.Store",
        "Microsoft.WinJS",
        "Microsoft.Advertising",
    ];

    private static readonly HashSet<string> DesktopSupportedPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows.Desktop",
        "Windows.Universal",
        "Windows.Core",
    };

    internal static bool IsPlatformCompatible(CatalogPackage catalogPackage)
    {
        if (catalogPackage.Platforms.Count == 0)
        {
            return true;
        }

        return catalogPackage.Platforms.Any(p => DesktopSupportedPlatforms.Contains(p));
    }

    public InstallPlan Resolve(StoreProduct product, IReadOnlyList<Fe3Package> packages) =>
        Resolve(product, packages, RuntimeInformation.OSArchitecture);

    public InstallPlan Resolve(
        StoreProduct product,
        IReadOnlyList<Fe3Package> packages,
        Architecture hostArchitecture)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(packages);

        var incompatibleMonikers = new HashSet<string>(
            product.Packages
                .Where(cp => !IsPlatformCompatible(cp) && !string.IsNullOrWhiteSpace(cp.PackageFullName))
                .Select(cp => cp.PackageFullName!),
            StringComparer.OrdinalIgnoreCase);

        var candidates = packages
            .Where(p => !string.Equals(p.PackageType, "XAP", StringComparison.OrdinalIgnoreCase))
            .Where(p => !incompatibleMonikers.Contains(p.Moniker))
            .ToList();

        if (candidates.Count == 0)
        {
            throw new StoreApiException("No installable packages were found for this product.");
        }

        var groups = candidates
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var identity = product.PackageIdentityName
                       ?? product.PackageFamilyName?.Split('_').FirstOrDefault();

        Fe3Package? main = null;
        if (!string.IsNullOrWhiteSpace(identity))
        {
            main = groups
                .Where(g => string.Equals(g.Key, identity, StringComparison.OrdinalIgnoreCase))
                .Select(g => PickBest(g, hostArchitecture))
                .FirstOrDefault(p => p is not null);
        }

        main ??= groups
            .Where(g => !IsFramework(g.Key))
            .Select(g => PickBest(g, hostArchitecture))
            .Where(p => p is not null)
            .OrderByDescending(p => p!.Version ?? new Version(0, 0))
            .FirstOrDefault();

        main ??= groups
            .Select(g => PickBest(g, hostArchitecture))
            .Where(p => p is not null)
            .OrderByDescending(p => p!.Version ?? new Version(0, 0))
            .FirstOrDefault();

        if (main is null)
        {
            throw new StoreApiException(
                $"None of the available packages can be installed on this {hostArchitecture} device.");
        }

        var dependencies = new List<Fe3Package>();
        foreach (var group in groups)
        {
            if (string.Equals(group.Key, main.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var best = PickBest(group, hostArchitecture);
            if (best is not null)
            {
                dependencies.Add(best);
            }
        }

        return new InstallPlan
        {
            Product = product,
            MainPackage = main,
            Dependencies = dependencies,
        };
    }

    internal static Fe3Package? PickBest(IEnumerable<Fe3Package> packages, Architecture host)
    {
        return packages
            .Select(package => (package, score: ArchitectureScore(package.Architecture, host)))
            .Where(candidate => candidate.score >= 0)
            .OrderByDescending(candidate => candidate.score)
            .ThenByDescending(candidate => candidate.package.Version ?? new Version(0, 0))
            .Select(candidate => candidate.package)
            .FirstOrDefault();
    }

    /// <summary>Higher is better; negative means incompatible with the host.</summary>
    internal static int ArchitectureScore(string architecture, Architecture host)
    {
        var arch = (architecture ?? string.Empty).Trim().ToLowerInvariant();
        var neutral = arch is "" or "neutral" or "any";

        return host switch
        {
            Architecture.X64 => arch switch
            {
                "x64" => 100,
                _ when neutral => 60,
                "x86" => 20,
                _ => -1,
            },
            Architecture.X86 => arch switch
            {
                "x86" => 100,
                _ when neutral => 60,
                _ => -1,
            },
            Architecture.Arm64 => arch switch
            {
                "arm64" => 100,
                _ when neutral => 70,
                "arm" => 60,
                "x86" => 30,
                "x64" => 20,
                _ => -1,
            },
            Architecture.Arm => arch switch
            {
                "arm" => 100,
                _ when neutral => 60,
                _ => -1,
            },
            _ => neutral ? 100 : -1,
        };
    }

    private static bool IsFramework(string name) =>
        FrameworkPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
