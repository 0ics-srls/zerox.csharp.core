using Avalonia.Controls;
using Avalonia.Threading;

namespace Zerox.Core.Avalonia.Dialogs;

// Message windows on the UI thread: modal on the owner when there is one, plain otherwise.
public sealed class DialogService(Func<Window?>? owner = null) : IDialogService
{
    // Raised on the UI thread once the window is shown.
    internal event Action<MessageWindow>? Opened;

    public Task<bool> ShowAsync(MessageKind kind, string message, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Dispatcher.UIThread.InvokeAsync(() => ShowOnUiAsync(kind, message, title ?? DefaultTitle(kind)));
    }

    private async Task<bool> ShowOnUiAsync(MessageKind kind, string message, string title)
    {
        var window = new MessageWindow(kind, title, message);
        var parent = owner?.Invoke();
        Task shown;
        if (parent is not null)
        {
            shown = window.ShowDialog(parent);
        }
        else
        {
            window.Show();
            shown = Task.CompletedTask;
        }
        Opened?.Invoke(window);
        await shown.ConfigureAwait(true);
        return await window.Result.ConfigureAwait(true);
    }

    private static string DefaultTitle(MessageKind kind) => kind switch
    {
        MessageKind.Notice => "Notice",
        MessageKind.Warning => "Warning",
        MessageKind.Error => "Error",
        MessageKind.Question => "Confirm",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown message kind"),
    };
}
