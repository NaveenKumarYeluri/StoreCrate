namespace StoreCrate.Core.Services;

/// <summary>Flattens an exception chain into a single, user-readable message.</summary>
public static class ErrorDetails
{
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message?.Trim();
            if (!string.IsNullOrEmpty(message) &&
                (messages.Count == 0 || !string.Equals(messages[^1], message, StringComparison.Ordinal)))
            {
                messages.Add(message);
            }
        }

        return messages.Count == 0 ? exception.GetType().Name : string.Join(" ", messages);
    }
}
