using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StoreCrate.Cli;
using StoreCrate.Core.Services;
using StoreCrate.Windows;

Console.OutputEncoding = Encoding.UTF8;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
}).SetMinimumLevel(LogLevel.Warning));

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

await using var provider = services.BuildServiceProvider();
await provider.GetRequiredService<ISettingsService>().LoadAsync();

return await CliApp.RunAsync(args, provider);
