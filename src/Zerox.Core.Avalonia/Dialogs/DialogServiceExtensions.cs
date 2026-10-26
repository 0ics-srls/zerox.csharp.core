namespace Zerox.Core.Avalonia.Dialogs;

public static class DialogServiceExtensions
{
    public static Task NotifyAsync(this IDialogService dialogs, string message)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        return dialogs.ShowAsync(MessageKind.Notice, message);
    }

    public static Task<bool> ConfirmAsync(this IDialogService dialogs, string question)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        return dialogs.ShowAsync(MessageKind.Question, question);
    }

    public static Task ShowErrorAsync(this IDialogService dialogs, string message)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        return dialogs.ShowAsync(MessageKind.Error, message);
    }

    // An error the code handled and wants the user to see.
    public static Task ShowErrorAsync(this IDialogService dialogs, Exception error)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(error);
        return dialogs.ShowAsync(MessageKind.Error, error.Message);
    }
}
