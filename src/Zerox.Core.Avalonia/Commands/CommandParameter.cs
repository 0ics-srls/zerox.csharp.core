namespace Zerox.Core.Avalonia.Commands;

internal static class CommandParameter
{
    // null → default; another type is a misuse.
    public static T? Cast<T>(object? parameter) => parameter switch
    {
        null => default,
        T value => value,
        _ => throw new ArgumentException($"The command takes a {typeof(T).Name}, not a {parameter.GetType().Name}", nameof(parameter)),
    };
}
