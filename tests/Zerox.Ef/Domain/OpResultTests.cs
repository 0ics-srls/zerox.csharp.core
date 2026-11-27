using Zerox.Ef.Core;

namespace Zerox.Ef.Domain.Tests;

// The outcome of an operation: a status, the value, the reason, the exception when one happened.
public sealed class OpResultTests
{
    public static TheoryData<OpStatus> WithReason => [OpStatus.Conflict, OpStatus.Invalid, OpStatus.Failed];

    public static TheoryData<OpStatus, string> BlankReasons => new()
    {
        { OpStatus.Conflict, "" },
        { OpStatus.Conflict, "  " },
        { OpStatus.Invalid, "" },
        { OpStatus.Invalid, "  " },
        { OpStatus.Failed, "" },
        { OpStatus.Failed, "  " },
    };

    private static OpResult<string> ByReason(OpStatus status, string reason) => status switch
    {
        OpStatus.Conflict => OpResult.Conflict<string>(reason),
        OpStatus.Invalid => OpResult.Invalid<string>(reason),
        OpStatus.Failed => OpResult.Failed<string>(reason),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Not a status with a reason."),
    };

    [Fact]
    public void Should_CarryTheValue_WhenOk()
    {
        var result = OpResult.Ok(42);

        result.Should().Be(new OpResult<int>(OpStatus.Ok, 42, null, null));
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Should_CarryTheValue_WhenCreated()
    {
        var result = OpResult.Created("x");

        result.Status.Should().Be(OpStatus.Created);
        result.Value.Should().Be("x");
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Should_CarryNoValue_WhenDeleted()
    {
        var result = OpResult.Deleted<string>();

        result.Status.Should().Be(OpStatus.Deleted);
        result.Value.Should().BeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Should_CarryNoValue_WhenNotFound()
    {
        var result = OpResult.NotFound<string>();

        result.Status.Should().Be(OpStatus.NotFound);
        result.Value.Should().BeNull();
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(WithReason))]
    public void Should_CarryTheReason_WhenConflictInvalidOrFailed(OpStatus status)
    {
        var result = ByReason(status, "the reason");

        result.Status.Should().Be(status);
        result.Reason.Should().Be("the reason");
        result.Value.Should().BeNull();
        result.Exception.Should().BeNull();
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(BlankReasons))]
    public void Should_RefuseABlankReason_WhenConflictInvalidOrFailed(OpStatus status, string reason)
    {
        var build = () => ByReason(status, reason);

        build.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_CarryTheException_WhenFaulted()
    {
        var error = new InvalidOperationException("boom");

        var result = OpResult.Faulted<int>(error);

        result.Status.Should().Be(OpStatus.Faulted);
        result.Exception.Should().BeSameAs(error);
        result.Reason.Should().Be("boom");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Should_RefuseANullException_WhenFaulted()
    {
        // A guard test: null goes into a parameter that is not nullable (the rule's own exception for tests).
        // ast-grep-ignore: no-null-forgiving
        var build = () => OpResult.Faulted<int>(null!);

        build.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Should_BeSuccessOnlyForOkCreatedDeleted()
    {
        var successes = Enum.GetValues<OpStatus>()
            .Where(status => new OpResult<int>(status, 0, null, null).IsSuccess);

        successes.Should().BeEquivalentTo([OpStatus.Ok, OpStatus.Created, OpStatus.Deleted]);
    }
}
