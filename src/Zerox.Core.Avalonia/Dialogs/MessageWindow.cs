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
