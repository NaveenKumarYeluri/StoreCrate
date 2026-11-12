using Microsoft.Extensions.DependencyInjection;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.Cli;

internal static class CliApp
{
    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "search" => await SearchAsync(services, rest),
                "info" => await InfoAsync(services, rest),
                "packages" => await PackagesAsync(services, rest),
                "download" => await DownloadAsync(services, rest),
                "install" => await InstallAsync(services, rest),
                "installed" => await InstalledAsync(services, rest),
                "uninstall" => await UninstallAsync(services, rest),
                "update" => await UpdateAsync(services),
                _ => Unknown(command),
            };
        }
        catch (StoreApiException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"network error: {ex.Message}");
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("cancelled.");
            return 130;
        }
    }

    private static async Task<int> SearchAsync(IServiceProvider services, string[] args)
    {
        var query = string.Join(' ', args);
        var catalog = services.GetRequiredService<IStoreCatalogClient>();
        var results = await catalog.SearchAsync(query);

        if (results.Count == 0)
        {
            Console.WriteLine("No results.");
            return 0;
        }

        Console.WriteLine($"{"ProductId",-16} {"Rating",-7} {"Price",-12} Title");
        foreach (var result in results)
        {
            Console.WriteLine(
                $"{result.ProductId,-16} {result.AverageRating?.ToString("0.0") ?? "-",-7} {result.Price ?? "-",-12} {result.Title}");
        }

        return 0;
    }

    private static async Task<int> InfoAsync(IServiceProvider services, string[] args)
    {
        var product = await RequireProductAsync(services, args);
        if (product is null)
        {
            return 1;
        }

        Console.WriteLine($"Title:        {product.Title}");
        Console.WriteLine($"ProductId:    {product.ProductId}");
        Console.WriteLine($"Publisher:    {product.Publisher}");
        Console.WriteLine($"Version:      {product.LatestVersionText ?? "-"}");
        Console.WriteLine($"Size:         {product.SizeText}");
        Console.WriteLine($"Rating:       {product.AverageRating?.ToString("0.0") ?? "-"} ({product.RatingCount ?? 0} ratings)");
        Console.WriteLine($"Price:        {product.Price ?? "-"}");
        Console.WriteLine($"Category:     {product.PrimaryCategory ?? "-"}");
        Console.WriteLine($"FamilyName:   {product.PackageFamilyName}");
        Console.WriteLine($"WuCategoryId: {product.WuCategoryId}");
        Console.WriteLine(product.ShortDescription ?? product.Description);
        return 0;
    }

    private static async Task<int> PackagesAsync(IServiceProvider services, string[] args)
    {
        var (product, plan) = await ResolvePlanAsync(services, args);
        if (plan is null)
        {
            return 1;
        }

        Console.WriteLine($"Install plan for {product!.Title} ({product.ProductId})");
        Console.WriteLine($"Main: {plan.MainPackage.Moniker}");
        Console.WriteLine($"URL:  {plan.MainPackage.Url}");
        foreach (var dependency in plan.Dependencies)
        {
            Console.WriteLine($"Dep:  {dependency.Moniker}");
        }

        return 0;
    }

    private static async Task<int> DownloadAsync(IServiceProvider services, string[] args)
    {
        var (product, plan) = await ResolvePlanAsync(services, args);
        if (plan is null)
        {
            return 1;
        }

        var settings = services.GetRequiredService<ISettingsService>();
        var downloadService = services.GetRequiredService<IDownloadService>();
        var directory = Path.Combine(settings.DownloadDirectory, product!.ProductId);
        Directory.CreateDirectory(directory);

        foreach (var package in plan.AllPackages)
        {
            Console.WriteLine($"Downloading {package.Moniker}");
            var progress = new Progress<DownloadProgress>(p =>
                Console.Write($"\r  {p.Percent,3}%  {p.SizeText}          "));
            var downloaded = await downloadService.DownloadAsync(package, directory, progress);
            Console.WriteLine($"\r  saved {downloaded.FilePath}");
        }

        Console.WriteLine($"Done. Files are in {directory}");
        return 0;
    }

    private static async Task<int> InstallAsync(IServiceProvider services, string[] args)
    {
        var (product, plan) = await ResolvePlanAsync(services, args);
        if (plan is null)
        {
            return 1;
        }

        var settings = services.GetRequiredService<ISettingsService>();
        var downloadService = services.GetRequiredService<IDownloadService>();
        var installer = services.GetRequiredService<IAppInstaller>();

        var directory = Path.Combine(settings.DownloadDirectory, product!.ProductId);
        Directory.CreateDirectory(directory);

        var downloaded = new List<DownloadedPackage>();
        foreach (var package in plan.AllPackages)
        {
            Console.WriteLine($"Downloading {package.Moniker}");
            downloaded.Add(await downloadService.DownloadAsync(package, directory));
        }

        Console.WriteLine("Installing...");
        var progress = new Progress<double>(p => Console.Write($"\r  {p * 100,3:0}%"));
        var result = await installer.InstallAsync(plan, downloaded, progress);
        Console.WriteLine();
        Console.WriteLine($"{result.Status}: {result.Message}");

        if (result.IsSuccess && !settings.Current.KeepDownloadedPackages)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Best effort.
            }
        }

        return result.IsSuccess ? 0 : 2;
    }

    private static async Task<int> InstalledAsync(IServiceProvider services, string[] args)
    {
        var includeSystem = args.Any(a => a is "--all" or "-a");
        var installedService = services.GetRequiredService<IInstalledAppService>();
        var apps = await installedService.GetInstalledAppsAsync(includeSystem);

        if (apps.Count == 0)
        {
            Console.WriteLine("No apps found.");
            return 0;
        }

        Console.WriteLine($"{"Version",-16} {"FamilyName",-48} Name");
        foreach (var app in apps)
        {
            Console.WriteLine($"{app.Version,-16} {app.PackageFamilyName,-48} {app.DisplayName}");
        }

        return 0;
    }

    private static async Task<int> UninstallAsync(IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: uninstall <packageFamilyName>");
            return 1;
        }

        var installedService = services.GetRequiredService<IInstalledAppService>();
        var app = await installedService.FindInstalledAsync(args[0]);
        if (app is null)
        {
            Console.Error.WriteLine($"Not installed: {args[0]}");
            return 1;
        }

        var result = await installedService.UninstallAsync(app);
        Console.WriteLine($"{result.Status}: {result.Message}");
        return result.IsSuccess ? 0 : 2;
    }

    private static async Task<int> UpdateAsync(IServiceProvider services)
    {
        var checker = services.GetRequiredService<IUpdateChecker>();
        var update = await checker.CheckAsync();
        if (update is null)
        {
            Console.WriteLine($"StoreCrate {AppInfo.VersionString} is up to date.");
            return 0;
        }

        Console.WriteLine($"Update available: {update.Tag}");
        Console.WriteLine(update.ReleaseUrl);
        return 0;
    }

    private static async Task<(StoreProduct? Product, InstallPlan? Plan)> ResolvePlanAsync(
        IServiceProvider services,
        string[] args)
    {
        var product = await RequireProductAsync(services, args);
        if (product is null)
        {
            return (null, null);
        }

        if (string.IsNullOrWhiteSpace(product.WuCategoryId))
        {
            Console.Error.WriteLine("This product does not expose a delivery category.");
            return (product, null);
        }

        var fe3 = services.GetRequiredService<IFe3Client>();
        var resolver = services.GetRequiredService<IPackageResolver>();
        var packages = await fe3.GetPackagesAsync(product.WuCategoryId);
        return (product, resolver.Resolve(product, packages));
    }

    private static async Task<StoreProduct?> RequireProductAsync(IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: <command> <productId>");
            return null;
        }

        var catalog = services.GetRequiredService<IStoreCatalogClient>();
        var product = await catalog.GetProductAsync(args[0]);
        if (product is null)
        {
            Console.Error.WriteLine($"Product not found: {args[0]}");
        }

        return product;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 1;
    }

    private static bool IsHelp(string arg) => arg is "-h" or "--help" or "help" or "/?";

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            StoreCrate CLI - headless Store client for testing the install pipeline

            Usage:
              storecrate search <query>            Search the Store catalog
              storecrate info <productId>          Show product details
              storecrate packages <productId>      Resolve packages for this device
              storecrate download <productId>      Download packages without installing
              storecrate install <productId>       Download and install a product
              storecrate installed [--all]         List installed apps
              storecrate uninstall <familyName>    Remove an installed app
              storecrate update                    Check for a StoreCrate update

            Examples:
              storecrate search spotify
              storecrate install 9NCBCSZSJRSB
            """);
    }
}
