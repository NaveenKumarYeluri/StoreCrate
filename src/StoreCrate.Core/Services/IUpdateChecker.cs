namespace StoreCrate.Core.Services;

/// <summary>Describes a newer GitHub release, if one exists.</summary>
public sealed record AppUpdateInfo(
    Version Version,
    string Tag,
    string? ReleaseUrl,
    string? Notes,
    string? DownloadUrl);

/// <summary>Checks GitHub Releases for a newer version of StoreCrate.</summary>
public interface IUpdateChecker
{
    Task<AppUpdateInfo?> CheckAsync(CancellationToken cancellationToken = default);
}
