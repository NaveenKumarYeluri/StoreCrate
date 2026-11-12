using CommunityToolkit.Mvvm.ComponentModel;

namespace StoreCrate.App.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;

    public void ReportError(Exception exception) =>
        ErrorMessage = exception is StoreCrate.Core.Services.StoreApiException
            ? exception.Message
            : $"Something went wrong: {exception.Message}";

    protected void ClearError() => ErrorMessage = null;
}
