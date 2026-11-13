using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using StoreCrate.App.ViewModels;

namespace StoreCrate.App;

public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var typeName = param.GetType().FullName
            ?.Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        if (typeName is null)
        {
            return new TextBlock { Text = "Invalid view model." };
        }

        var type = Type.GetType(typeName);
        if (type is null)
        {
            return new TextBlock { Text = $"Not Found: {typeName}" };
        }

        try
        {
            return (Control)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            // A throwing constructor used to leave the previous page on screen with no
            // explanation; surface the failure instead.
            return new TextBlock
            {
                Text = $"Failed to load {typeName}: {ex.Message}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(24),
            };
        }
    }

    public bool Match(object? data) => data is ViewModelBase;
}
