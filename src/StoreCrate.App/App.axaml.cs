using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using StoreCrate.App.Services;
using StoreCrate.App.ViewModels;
using StoreCrate.App.Views;
using StoreCrate.Core.Models;
using StoreCrate.Core.Services;

namespace StoreCrate.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        AppServices.Initialize();

        var settings = AppServices.Get<ISettingsService>();
        try
        {
            settings.LoadAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Core.Logging.AppLog.Warning($"Settings could not be loaded; using defaults. {ex.Message}", ex);
        }

        Core.Logging.AppLog.Info($"Startup: services ready in {stopwatch.ElapsedMilliseconds} ms (theme: {settings.Current.Theme})");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Core.Logging.AppLog.Error("Unhandled exception", args.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Core.Logging.AppLog.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        ApplyTheme(settings.Current.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = AppServices.Get<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.MainWindow.Opened += (_, _) =>
                Core.Logging.AppLog.Info($"Startup: window shown after {stopwatch.ElapsedMilliseconds} ms");
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(string theme)
    {
        if (Current is null)
        {
            return;
        }

        Current.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
