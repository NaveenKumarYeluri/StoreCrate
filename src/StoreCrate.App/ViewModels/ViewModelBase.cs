using CommunityToolkit.Mvvm.ComponentModel;

namespace StoreCrate.App.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;

    public void ReportError(Exception exception)
    {
        var details = StoreCrate.Core.Services.ErrorDetails.Describe(exception);
        ErrorMessage = exception is StoreCrate.Core.Services.StoreApiException
            ? details
            : $"Something went wrong: {details}";
    }

    protected void ClearError() => ErrorMessage = null;
}
