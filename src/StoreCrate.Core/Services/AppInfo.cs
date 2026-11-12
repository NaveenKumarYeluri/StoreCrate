namespace StoreCrate.Core.Services;

/// <summary>Static application metadata used for branding and update checks.</summary>
public static class AppInfo
{
    public const string Name = "StoreCrate";

    public const string Tagline = "A lightweight, modern Microsoft Store client for Windows.";

    public const string RepositoryOwner = "naveenk0513";

    public const string RepositoryName = "StoreCrate";

    public static string RepositoryUrl => $"https://github.com/{RepositoryOwner}/{RepositoryName}";

    public static Version Version =>
        typeof(AppInfo).Assembly.GetName().Version ?? new Version(0, 1, 0);

    public static string VersionString
    {
        get
        {
            var version = Version;
            return version.Revision > 0
                ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
                : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
