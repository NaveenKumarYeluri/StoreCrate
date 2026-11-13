using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using StoreCrate.Cli;
using StoreCrate.Core.Logging;
using StoreCrate.Core.Services;
using StoreCrate.Windows;

Console.OutputEncoding = Encoding.UTF8;

AppLog.Initialize();

var services = new ServiceCollection();
services.AddLogging(builder => builder
    .AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    })
    .AddProvider(new FileLoggerProvider())
    .SetMinimumLevel(LogLevel.Information)
    // Keep the console terse (warnings and errors only); the file log gets everything.
    .AddFilter<ConsoleLoggerProvider>(null, LogLevel.Warning)
    // The catalog client already logs requests once with useful durations.
    .AddFilter("System.Net.Http.HttpClient", LogLevel.Warning)
    .AddFilter("Microsoft.Extensions.Http", LogLevel.Warning));

services.AddSingleton<ISettingsService, SettingsService>();
services.AddSingleton<IFe3Client, Fe3Client>();
services.AddSingleton<IPackageResolver, PackageResolver>();
services.AddSingleton<IAppInstaller, PackageInstaller>();
services.AddSingleton<IInstalledAppService, InstalledAppService>();
services.AddSingleton<IAppUpdateService, AppUpdateService>();

services.AddHttpClient<IStoreCatalogClient, StoreCatalogClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

services.AddHttpClient<IDownloadService, DownloadService>(client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});

services.AddHttpClient<IUpdateChecker, GitHubUpdateChecker>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

await using var provider = services.BuildServiceProvider();
await provider.GetRequiredService<ISettingsService>().LoadAsync();

return await CliApp.RunAsync(args, provider);
