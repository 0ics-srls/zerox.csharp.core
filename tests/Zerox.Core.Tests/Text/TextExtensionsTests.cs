using Zerox.Core.Text;

namespace Zerox.Core.Tests.Text;

public sealed class TextExtensionsTests
{
    [Fact]
    public void Should_KeepTheText_WhenItFitsTheLimit()
    {
        "abc".Truncate(5).Should().Be("abc");
        "abc".Clip(3).Should().Be("abc");
    }

    [Fact]
    public void Should_EndWithEllipsisWithinTheLimit_WhenTruncating()
        => "abcdefghij".Truncate(6).Should().Be("abc...").And.HaveLength(6);

    [Fact]
    public void Should_CutWithoutEllipsis_WhenTheLimitIsThreeOrLess()
    {
        "abcdef".Truncate(2).Should().Be("ab");
        "abcdef".Truncate(0).Should().BeEmpty();
    }

    [Fact]
    public void Should_NotLimit_WhenClipMaxLengthIsZeroOrNegative()
    {
        "abcdef".Clip(0).Should().Be("abcdef");
        "abcdef".Clip(-1).Should().Be("abcdef");
        ((string?)null).Clip(3).Should().BeNull();
    }

    [Fact]
    public void Should_RoundTripUtf8_WhenEncodingToBase64()
    {
        "naïve café €".ToBase64Utf8().FromBase64Utf8().Should().Be("naïve café €");
        "".FromBase64Utf8("x").Should().Be("x");
    }

    [Fact]
    public void Should_ThrowFormat_WhenTheBase64IsInvalid()
        => FluentActions.Invoking(() => "%%%".FromBase64Utf8()).Should().Throw<FormatException>();
}
