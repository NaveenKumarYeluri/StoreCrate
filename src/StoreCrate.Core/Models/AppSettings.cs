namespace StoreCrate.Core.Models;

/// <summary>User-configurable application settings, persisted as JSON.</summary>
public sealed class AppSettings
{
    public string Market { get; set; } = "US";

    public string Locale { get; set; } = "en-US";

    /// <summary>Directory used to stage downloaded packages. Null uses the default app data path.</summary>
    public string? DownloadDirectory { get; set; }

    /// <summary>One of <c>System</c>, <c>Light</c> or <c>Dark</c>.</summary>
    public string Theme { get; set; } = "System";

    public bool CheckForUpdatesOnStartup { get; set; } = true;

    /// <summary>Keep downloaded package files after a successful install.</summary>
    public bool KeepDownloadedPackages { get; set; }

    public bool ShowSystemApps { get; set; }

    public int MaxConcurrentDownloads { get; set; } = 2;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
