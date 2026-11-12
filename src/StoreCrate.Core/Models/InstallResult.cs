namespace StoreCrate.Core.Models;

public enum InstallStatus
{
    Success,
    NeedsAttention,
    Failed,
}

/// <summary>Outcome of an install attempt.</summary>
public sealed record InstallResult(InstallStatus Status, string Message, string? ErrorCode = null)
{
    public bool IsSuccess => Status == InstallStatus.Success;

    public static InstallResult Success(string message = "Installed successfully.") =>
        new(InstallStatus.Success, message);

    public static InstallResult NeedsAttention(string message, string? errorCode = null) =>
        new(InstallStatus.NeedsAttention, message, errorCode);

    public static InstallResult Failed(string message, string? errorCode = null) =>
        new(InstallStatus.Failed, message, errorCode);
}
