using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Zerox.Core.Avalonia.Dialogs;

namespace Zerox.Core.Avalonia.Tests.Dialogs;

public sealed class DialogServiceTests
{
    private sealed class Opening
    {
        public MessageWindow? Window { get; set; }

        public bool OnUi { get; set; }
    }

    private static (DialogService Service, Opening Opening) Service(bool withOwner = true)
    {
        Window? owner = null;
        if (withOwner)
        {
            owner = new Window();
            owner.Show();
        }
        var service = new DialogService(() => owner);
        var opening = new Opening();
        service.Opened += window =>
        {
            opening.Window = window;
            opening.OnUi = SynchronizationContext.Current is AvaloniaSynchronizationContext;
        };
        return (service, opening);
    }

    // Runs the posted jobs until the window is open (bounded).
    private static MessageWindow WaitOpen(Opening opening)
    {
        for (var i = 0; i < 100 && opening.Window is null; i++)
            Dispatcher.UIThread.RunJobs();
        opening.Window.Should().NotBeNull();
        return opening.Window!;
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public Task Should_ReturnTrue_WhenOkIsClicked() => HeadlessUi.RunAsync(async () =>
    {
        var (service, opening) = Service();

        var answer = service.ConfirmAsync("Go?");
        var window = WaitOpen(opening);
        window.Title.Should().Be("Confirm");
        window.CancelButton.Should().NotBeNull();
        Click(window.OkButton);

        (await answer).Should().BeTrue();
    });

    [Fact]
    public Task Should_ReturnFalse_WhenCancelIsClicked() => HeadlessUi.RunAsync(async () =>
    {
        var (service, opening) = Service();

        var answer = service.ConfirmAsync("Go?");
        Click(WaitOpen(opening).CancelButton!);

        (await answer).Should().BeFalse();
    });

    [Fact]
    public Task Should_OfferOnlyOk_WhenItIsANotice() => HeadlessUi.RunAsync(async () =>
    {
        var (service, opening) = Service();

        var shown = service.NotifyAsync("Done");
        var window = WaitOpen(opening);
        window.Title.Should().Be("Notice");
        window.Message.Should().Be("Done");
        window.CancelButton.Should().BeNull();
        Click(window.OkButton);

        await shown;
    });

    [Fact]
    public Task Should_ReturnFalse_WhenTheWindowIsClosed() => HeadlessUi.RunAsync(async () =>
    {
        var (service, opening) = Service();

        var answer = service.ConfirmAsync("Go?");
        WaitOpen(opening).Close();

        (await answer).Should().BeFalse();
    });

    [Fact]
    public Task Should_ShowTheExceptionMessage_WhenAnErrorIsShown() => HeadlessUi.RunAsync(async () =>
    {
        var (service, opening) = Service();

        var shown = service.ShowErrorAsync(new InvalidOperationException("disk full"));
        var window = WaitOpen(opening);
        window.Title.Should().Be("Error");
        window.Message.Should().Be("disk full");
        window.Kind.Should().Be(MessageKind.Error);
        Click(window.OkButton);

        await shown;
    });

    [Fact]
    public Task Should_ShowAPlainWindow_WhenThereIsNoOwner() => HeadlessUi.RunAsync(async () =>
    {
        var (service, opening) = Service(withOwner: false);

        var answer = service.ShowAsync(MessageKind.Warning, "Careful", "Warn");
        var window = WaitOpen(opening);
        window.Title.Should().Be("Warn");
        Click(window.OkButton);

        (await answer).Should().BeTrue();
    });

    [Fact]
    public Task Should_OpenOnTheUiThread_WhenCalledFromAnotherThread() => HeadlessUi.RunAsync(async () =>
    {
        var (service, opening) = Service();

        // StartNew does not unwrap: the outer task ends once the worker has made the call (and posted it).
        var shown = await Task.Factory.StartNew(() => service.NotifyAsync("From a worker"), TestContext.Current.CancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
        var window = WaitOpen(opening);
        opening.OnUi.Should().BeTrue();
        Click(window.OkButton);

        await shown;
    });
}
