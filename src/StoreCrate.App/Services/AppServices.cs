using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StoreCrate.App.Services;
using StoreCrate.App.ViewModels;
using StoreCrate.Core.Services;
using StoreCrate.Windows;

namespace StoreCrate.App.Services;

/// <summary>Composition root for the desktop app.</summary>
public static class AppServices
{
    private static IServiceProvider? _provider;

    public static IServiceProvider Provider =>
        _provider ?? throw new InvalidOperationException("AppServices has not been initialized.");

    public static T Get<T>() where T : notnull => Provider.GetRequiredService<T>();

    public static void Initialize()
    {
        if (_provider is not null)
        {
            return;
        }

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IFe3Client, Fe3Client>();
        services.AddSingleton<IPackageResolver, PackageResolver>();
        services.AddSingleton<IAppInstaller, PackageInstaller>();
        services.AddSingleton<IInstalledAppService, InstalledAppService>();
        services.AddSingleton<IAppUpdateService, AppUpdateService>();

        services.AddHttpClient<IStoreCatalogClient, StoreCatalogClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient<IDownloadService, DownloadService>(client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddHttpClient<IUpdateChecker, GitHubUpdateChecker>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddHttpClient<ImageCache>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<NavigationService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<DownloadManager>();

        services.AddSingleton<DiscoverViewModel>();
        services.AddSingleton<SearchViewModel>();
        services.AddTransient<DetailsViewModel>();
        services.AddSingleton<InstalledViewModel>();
        services.AddSingleton<DownloadsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        _provider = services.BuildServiceProvider();
    }
}
