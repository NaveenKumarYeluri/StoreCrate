using CommunityToolkit.Mvvm.ComponentModel;
using StoreCrate.Core.Models;

namespace StoreCrate.App.ViewModels;

public enum DownloadState
{
    Queued,
    Downloading,
    Installing,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>One queued install: download progress, install state and user actions.</summary>
public sealed partial class DownloadItem : ObservableObject
{
    public DownloadItem(InstallPlan plan)
    {
        Plan = plan;
        State = DownloadState.Queued;
        StatusText = "Queued";
        DetailText = $"{plan.AllPackages.Count} package(s)";
    }

    public InstallPlan Plan { get; }

    public string ProductId => Plan.Product.ProductId;

    public string Title => Plan.Product.Title;

    public string PublisherText => Plan.Product.PublisherText;

    public string? LogoUrl => Plan.Product.LogoUrl;

    public string PackageCountText => Plan.Dependencies.Count == 0
        ? "Main package only"
        : $"Main package + {Plan.Dependencies.Count} dependenc{(Plan.Dependencies.Count == 1 ? "y" : "ies")}";

    [ObservableProperty]
    private DownloadState _state;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _detailText = string.Empty;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsCompleted => State == DownloadState.Completed;

    public bool IsFailed => State == DownloadState.Failed;

    public bool IsQueued => State == DownloadState.Queued;

    public bool CanCancel => State is DownloadState.Queued or DownloadState.Downloading or DownloadState.Installing;

    public bool CanRetry => State is DownloadState.Failed or DownloadState.Cancelled;

    public bool CanOpen => State is DownloadState.Completed;

    public bool CanLaunch => CanOpen;

    public string PercentText => $"{Math.Clamp(Progress, 0, 100):0}%";

    partial void OnStateChanged(DownloadState value)
    {
        IsActive = value is DownloadState.Queued or DownloadState.Downloading or DownloadState.Installing;
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsQueued));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(CanOpen));
        OnPropertyChanged(nameof(CanLaunch));
    }

    partial void OnProgressChanged(double value) => OnPropertyChanged(nameof(PercentText));
}
