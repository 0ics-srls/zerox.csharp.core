# Dialogs

<block id="04-dialogs">
## TDDAB-4: Write the Minimal Dialog Service

<intro>
Delivers, in `src/Zerox.Core.Avalonia/Dialogs`, the minimal dialog service (user decision: notify,
confirm, error; Avalonia has no message box):
- `MessageKind` {Notice, Warning, Error, Question};
- `IDialogService`: `Task<bool> ShowAsync(MessageKind kind, string message, string? title = null)` —
  true when the user answered OK; a `Question` also offers Cancel; closing the window is not OK
  (view models get the service injected; tests substitute it);
- `DialogServiceExtensions` (static): `NotifyAsync(message)`, `ConfirmAsync(question)` → bool,
  `ShowErrorAsync(string message)`, `ShowErrorAsync(Exception error)` (shows `error.Message`: an
  error the code handled and wants the user to see — an unexpected exception is never caught to be
  shown, P9);
- `DialogService(Func<Window?>? owner = null)` (sealed): `ShowAsync` moves to the UI thread with
  `Dispatcher.UIThread.InvokeAsync`; builds a `MessageWindow`; with an owner (read at show time)
  it is modal (`ShowDialog(owner)`), without one it is a plain window (`Show()`); the answer comes
  when the window closes; default titles: Notice "Notice", Warning "Warning", Error "Error",
  Question "Confirm"; internal event `Opened(MessageWindow)` raised after the window is shown (the
  tests answer through it);
- internal `MessageWindow : Window` built in code (no XAML): the message (`TextBlock`, wrapping),
  an OK button (`IsDefault`), for a question a Cancel button (`IsCancel`); internal `Kind`,
  `Message`, `OkButton`, `CancelButton` (null when absent), `Result` (`Task<bool>` set when the
  window closes: true only after OK). The look comes from the application's theme.

Capability: old `../../tmp/zerox/Zerox.Wpf.Core/Core/Wpf/MessagingContext.cs` (`IMessagingContext`:
`Confirm`, `Notify`, `ShowError(Exception)`, `ShowError(string)` on WPF `MessageBox`, synchronous;
`MessageGravity` NORMAL/WARNING/ERROR/PROMPT; a `DummyMessagingContext` throwing
`NotImplementedException`). Async now (a dialog is awaited, the UI thread is not blocked); the
dummy is dropped (tests substitute `IDialogService`). `MessageServiceBase` (1234 lines) is not
ported now (user decision: only when a need appears).

Files: `src/Zerox.Core.Avalonia/Dialogs/MessageKind.cs`, `IDialogService.cs`,
`DialogServiceExtensions.cs`, `DialogService.cs`, `MessageWindow.cs`; tests
`tests/Zerox.Core.Avalonia.Tests/Dialogs/DialogServiceTests.cs` (an owner `Window` shown first;
the window answered by raising `Button.ClickEvent` on its buttons; posted jobs drained with
`Dispatcher.UIThread.RunJobs()`).
</intro>

<red>
- test: Should_ReturnTrue_WhenOkIsClicked — `ConfirmAsync("Go?")` with an owner: the window opens with title "Confirm" and both buttons; OK clicked → true
- test: Should_ReturnFalse_WhenCancelIsClicked — Cancel clicked → false
- test: Should_OfferOnlyOk_WhenItIsANotice — `NotifyAsync("Done")`: title "Notice", no Cancel button, OK clicked → the task completes
- test: Should_ReturnFalse_WhenTheWindowIsClosed — the question window closed without a button → false
- test: Should_ShowTheExceptionMessage_WhenAnErrorIsShown — `ShowErrorAsync(new InvalidOperationException("disk full"))`: title "Error", message "disk full"
- test: Should_ShowAPlainWindow_WhenThereIsNoOwner — the owner function returns null: the window opens, OK → true
- test: Should_OpenOnTheUiThread_WhenCalledFromAnotherThread — `NotifyAsync` called inside `Task.Run`: the window is opened on the UI thread (the `Opened` handler sees the Avalonia context); OK → the call completes
</red>

### Implementation

```csharp
namespace Zerox.Core.Avalonia.Dialogs;

public enum MessageKind
{
    Notice,
    Warning,
    Error,
    Question,
}
```

```csharp
namespace Zerox.Core.Avalonia.Dialogs;

// Messages to the user; view models get it injected, tests substitute it.
public interface IDialogService
{
    // True when the user answered OK (a question also offers Cancel; closing the window is not OK).
    Task<bool> ShowAsync(MessageKind kind, string message, string? title = null);
}
```

```csharp
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
```

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Zerox.Core.Avalonia.Dialogs;

// The message window, built in code; the look comes from the application's theme.
internal sealed class MessageWindow : Window
{
    private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _ok;

    public MessageWindow(MessageKind kind, string title, string message)
    {
        Kind = kind;
        Message = message;
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        OkButton = new Button { Content = "OK", IsDefault = true };
        OkButton.Click += (_, _) => Answer(true);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        buttons.Children.Add(OkButton);
        if (kind == MessageKind.Question)
        {
            CancelButton = new Button { Content = "Cancel", IsCancel = true };
            CancelButton.Click += (_, _) => Answer(false);
            buttons.Children.Add(CancelButton);
        }
        var body = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 480 });
        body.Children.Add(buttons);
        Content = body;
        Closed += (_, _) => _result.TrySetResult(_ok);
    }

    public MessageKind Kind { get; }

    public string Message { get; }

    public Button OkButton { get; }

    public Button? CancelButton { get; }

    // Set when the window closes: true only after OK.
    public Task<bool> Result => _result.Task;

    private void Answer(bool ok)
    {
        _ok = ok;
        Close(ok);
    }
}
```

```csharp
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
```

<success>
- [ ] `DialogServiceTests` pass (7 tests)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
