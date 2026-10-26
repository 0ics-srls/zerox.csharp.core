using System.Windows.Input;
using Avalonia.Threading;
using Zerox.Core.Avalonia.Commands;
using Zerox.Core.ComponentModel;

namespace Zerox.Core.Avalonia.Tests.Commands;

public sealed class CommandTests
{
    public sealed class Source : NotifyingObject
    {
        private int _value;

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    [Fact]
    public Task Should_RunTheAction_WhenItCanExecute() => HeadlessUi.RunAsync(() =>
    {
        string? got = null;
        var command = new Command<string>(p => got = p);

        command.Execute("a").Should().BeTrue();

        got.Should().Be("a");
        return Task.CompletedTask;
    });

    [Fact]
    public Task Should_NotRun_WhenItCannotExecute() => HeadlessUi.RunAsync(() =>
    {
        var ran = false;
        var command = new Command<string>(_ => ran = true, _ => false);

        command.CanExecute("a").Should().BeFalse();
        command.Execute("a").Should().BeFalse();

        ran.Should().BeFalse();
        return Task.CompletedTask;
    });

    [Fact]
    public Task Should_RefuseItself_WhenExecutedFromInsideItsAction() => HeadlessUi.RunAsync(() =>
    {
        Command<string>? command = null;
        bool? nested = null;
        command = new Command<string>(_ => nested = command!.Execute("again"));

        command.Execute("first").Should().BeTrue();

        nested.Should().BeFalse();
        return Task.CompletedTask;
    });

    [Fact]
    public Task Should_ReQueryOnTheUiThread_WhenTheSourceChanges() => HeadlessUi.RunAsync(async () =>
    {
        var source = new Source();
        var command = new Command<string>(_ => { }, _ => source.Value > 0, requery: source);
        var raised = new List<bool>();
        command.CanExecuteChanged += (_, _) => raised.Add(SynchronizationContext.Current is AvaloniaSynchronizationContext);

        await Task.Run(() => source.Value = 1);
        Dispatcher.UIThread.RunJobs();

        raised.Should().Equal(true);
        command.CanExecute("a").Should().BeTrue();
    });

    [Fact]
    public Task Should_KeepLabelAndIcon_WhenCreated() => HeadlessUi.RunAsync(() =>
    {
        ICommandWithInfo command = Command.Create(() => { }, label: "Save", icon: "save");

        command.Label.Should().Be("Save");
        command.Icon.Should().Be("save");
        command.CanExecute(null).Should().BeTrue();
        return Task.CompletedTask;
    });

    [Fact]
    public Task Should_RejectTheParameter_WhenItHasAnotherType() => HeadlessUi.RunAsync(() =>
    {
        string? got = "not called";
        ICommand command = new Command<string>(p => got = p);

        FluentActions.Invoking(() => command.Execute(42)).Should().Throw<ArgumentException>();
        command.Execute(null);

        got.Should().BeNull();
        return Task.CompletedTask;
    });

    [Fact]
    public Task Should_LetTheExceptionReachTheCaller_WhenTheActionThrows() => HeadlessUi.RunAsync(() =>
    {
        var fail = true;
        var command = new Command<string>(_ =>
        {
            if (fail)
                throw new InvalidOperationException("broken");
        });

        FluentActions.Invoking(() => command.Execute("a")).Should().Throw<InvalidOperationException>().WithMessage("broken");

        fail = false;
        command.Execute("a").Should().BeTrue();
        return Task.CompletedTask;
    });
}
