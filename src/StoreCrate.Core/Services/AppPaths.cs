namespace StoreCrate.Core.Services;

/// <summary>Well-known local folders used by StoreCrate.</summary>
public static class AppPaths
{
    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StoreCrate");

    public static string SettingsFile => Path.Combine(AppDataDirectory, "settings.json");

    public static string DefaultDownloadDirectory => Path.Combine(AppDataDirectory, "downloads");

    public static string CacheDirectory => Path.Combine(AppDataDirectory, "cache");

    public static string LogDirectory => Path.Combine(AppDataDirectory, "logs");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
