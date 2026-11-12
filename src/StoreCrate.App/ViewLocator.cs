using Avalonia.Controls;
using Avalonia.Controls.Templates;
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

        return (Control)Activator.CreateInstance(type)!;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
