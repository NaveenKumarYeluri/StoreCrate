namespace StoreCrate.App.Services;

public enum NavigationTarget
{
    Discover,
    Search,
    Installed,
    Downloads,
    Settings,
    Details,
}

public sealed record NavigationRequest(NavigationTarget Target, string? ProductId = null);

/// <summary>Routes navigation requests from view models to the main window.</summary>
public sealed class NavigationService
{
    public event EventHandler<NavigationRequest>? Requested;

    public event EventHandler? BackRequested;

    public void Navigate(NavigationTarget target) =>
        Requested?.Invoke(this, new NavigationRequest(target));

    public void ShowProduct(string productId)
    {
        if (!string.IsNullOrWhiteSpace(productId))
        {
            Requested?.Invoke(this, new NavigationRequest(NavigationTarget.Details, productId));
        }
    }

    public void Back() => BackRequested?.Invoke(this, EventArgs.Empty);
}
