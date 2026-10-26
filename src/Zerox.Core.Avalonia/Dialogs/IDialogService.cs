namespace Zerox.Core.Avalonia.Dialogs;

// Messages to the user; view models get it injected, tests substitute it.
public interface IDialogService
{
    // True when the user answered OK (a question also offers Cancel; closing the window is not OK).
    Task<bool> ShowAsync(MessageKind kind, string message, string? title = null);
}
