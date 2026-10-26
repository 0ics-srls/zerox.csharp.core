using Zerox.Core.Concurrency;

namespace Zerox.Core.Tests.Concurrency;

public sealed class AtomicBooleanTests
{
    [Fact]
    public void TestAtomicBoolean()
    {
        var b = new AtomicBoolean();
        Assert.False(b.Value);
        b = new AtomicBoolean(false);
        Assert.False(b.Value);
        b = new AtomicBoolean(true);
        Assert.True(b.Value);
        // when the value is already true, FalseToTrue fails
        b.Value = true;
        Assert.False(b.FalseToTrue());
        Assert.True(b.Value);
        // when the value is already false, TrueToFalse fails
        b.Value = false;
        Assert.False(b.TrueToFalse());
        Assert.False(b.Value);
        // the value does not change when SetWhen fails
        b.Value = false;
        Assert.False(b.SetWhen(true, true));
        Assert.False(b.Value);
        b.Value = true;
        Assert.False(b.SetWhen(false, false));
        Assert.True(b.Value);
    }

    [Fact]
    public void Should_RunTheCallbackOnce_WhenTheValueChanges()
    {
        var calls = 0;
        var b = new AtomicBoolean(false, () => calls++);

        b.Value = true;
        b.Value = true;

        calls.Should().Be(1);
    }

    [Fact]
    public void Should_NotRunTheCallback_WhenSetWhenChangesNothing()
    {
        var calls = 0;
        var b = new AtomicBoolean(true, () => calls++);

        b.SetWhen(true, true).Should().BeTrue();

        calls.Should().Be(0);
        b.Value.Should().BeTrue();
    }

    [Fact]
    public void Should_LetExactlyOneCallerWin_WhenManyCallFalseToTrueTogether()
    {
        var b = new AtomicBoolean();
        var winners = 0;

        Parallel.For(0, 1000, _ =>
        {
            if (b.FalseToTrue())
                Interlocked.Increment(ref winners);
        });

        winners.Should().Be(1);
    }
}
