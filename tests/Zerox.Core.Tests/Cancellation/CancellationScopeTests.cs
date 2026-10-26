using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Cancellation;

namespace Zerox.Core.Tests.Cancellation;

public sealed class CancellationScopeTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task Should_StayActive_WhenTheWorkEndsBeforeTimeoutAndCancel()
    {
        using var terminator = new Terminator(_time);
        using var scope = terminator.CreateScope(TimeSpan.FromSeconds(3));
        var delay = scope.DelayAsync(TimeSpan.FromSeconds(1));

        _time.Advance(TimeSpan.FromSeconds(1));

        await delay;
        scope.State.Should().Be(CancellationState.Active);
        scope.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Should_BeCanceled_WhenCanceledExplicitly()
    {
        using var terminator = new Terminator(_time);
        using var scope = terminator.CreateScope(TimeSpan.FromSeconds(30));
        var delay = scope.DelayAsync(TimeSpan.FromSeconds(10));

        scope.Cancel();

        await FluentActions.Awaiting(() => delay).Should().ThrowAsync<OperationCanceledException>();
        scope.State.Should().Be(CancellationState.Canceled);
    }

    [Fact]
    public async Task Should_BeTimedOut_WhenTheTimeoutElapses()
    {
        using var terminator = new Terminator(_time);
        using var scope = terminator.CreateScope(TimeSpan.FromSeconds(3));
        var delay = scope.DelayAsync(TimeSpan.FromSeconds(10));

        _time.Advance(TimeSpan.FromSeconds(3));

        await FluentActions.Awaiting(() => delay).Should().ThrowAsync<OperationCanceledException>();
        scope.State.Should().Be(CancellationState.TimedOut);
    }

    [Fact]
    public void Should_CancelEveryScope_WhenTheTerminatorCancels()
    {
        using var terminator = new Terminator(_time);
        using var a = terminator.CreateScope();
        using var b = terminator.CreateScope();
        using var longChild = a.CreateChild(TimeSpan.FromSeconds(10));
        using var shortChild = a.CreateChild(TimeSpan.FromMilliseconds(500));

        _time.Advance(TimeSpan.FromMilliseconds(500));
        shortChild.State.Should().Be(CancellationState.TimedOut);
        terminator.CancelAll();

        foreach (var scope in new[] { a, b, longChild, shortChild })
        {
            scope.State.Should().Be(CancellationState.Canceled);
            scope.Token.IsCancellationRequested.Should().BeTrue();
        }
    }

    [Fact]
    public void Should_BeCanceled_WhenTheParentIsCanceled()
    {
        using var terminator = new Terminator(_time);
        using var parent = terminator.CreateScope();
        using var child = parent.CreateChild();

        parent.Cancel();

        child.State.Should().Be(CancellationState.Canceled);
        child.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Should_ForgetTheScope_WhenDisposed()
    {
        using var terminator = new Terminator(_time);
        terminator.Count.Should().Be(0);
        var first = terminator.CreateScope();
        var second = terminator.CreateScope();
        terminator.Count.Should().Be(2);
        var child = first.CreateChild();
        first.Count.Should().Be(1);

        child.Dispose();
        first.Count.Should().Be(0);
        first.Dispose();
        second.Dispose();

        terminator.Count.Should().Be(0);
    }

    [Fact]
    public void Should_DisposeTheChildren_WhenTheParentIsDisposed()
    {
        using var terminator = new Terminator(_time);
        var parent = terminator.CreateScope();
        parent.CreateChild();
        parent.CreateChild();

        parent.Dispose();

        parent.Count.Should().Be(0);
        FluentActions.Invoking(() => parent.CreateChild()).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Should_Throw_WhenThrowIfCanceledAfterCancel()
    {
        using var terminator = new Terminator(_time);
        using var scope = terminator.CreateScope();

        FluentActions.Invoking(scope.ThrowIfCanceled).Should().NotThrow();
        scope.Cancel();
        FluentActions.Invoking(scope.ThrowIfCanceled).Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Should_LetTheCallbackExceptionReachTheCaller_WhenACancellationCallbackThrows()
    {
        using var terminator = new Terminator(_time);
        using var scope = terminator.CreateScope();
        using var registration = scope.Token.Register(() => throw new InvalidOperationException("callback failed"));

        FluentActions.Invoking(scope.Cancel).Should().Throw<AggregateException>()
            .Which.InnerExceptions.Should().ContainSingle(e => e is InvalidOperationException && e.Message == "callback failed");
    }
}
