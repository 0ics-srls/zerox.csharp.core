using System.Globalization;
using System.Numerics;
using Zerox.Core.Numbers;

namespace Zerox.Core.Tests.Numbers;

public sealed class NumberRangeTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Fact]
    public void Should_ClampToTheTypeLimits_WhenTheTextIsBeyondThem()
    {
        CheckLimits<sbyte>();
        CheckLimits<byte>();
        CheckLimits<short>();
        CheckLimits<ushort>();
        CheckLimits<int>();
        CheckLimits<uint>();
        CheckLimits<long>();
        CheckLimits<ulong>();
    }

    [Theory]
    [InlineData("50", 10)]
    [InlineData("-50", -10)]
    [InlineData("5", 5)]
    public void Should_ClampToTheInterval_WhenTheTextIsOutsideIt(string text, int expected)
    {
        new NumberRange<int>(-10, 10).TryParse(text, Invariant, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Fact]
    public void Should_GiveNoValue_WhenTheTextIsNotANumber()
    {
        NumberRange.Full<int>().TryParse("abc", Invariant, out _).Should().BeFalse();
        NumberRange.Full<int>().TryParse("", Invariant, out _).Should().BeFalse();
        NumberRange.Full<int>().TryParse("1.5", Invariant, out _).Should().BeFalse();
        NumberRange.Full<double>().TryParse("1.5", Invariant, out var value).Should().BeTrue();
        value.Should().Be(1.5);
    }

    [Fact]
    public void Should_SaturateAtTheBounds_WhenStepping()
    {
        var range = new NumberRange<int>(0, 100);
        range.Add(95, 10).Should().Be(100);
        range.Add(5, -10).Should().Be(0);
        range.Add(50, 10).Should().Be(60);

        NumberRange.Full<long>().Add(long.MaxValue - 1, 10).Should().Be(long.MaxValue);
        NumberRange.Full<long>().Add(long.MinValue + 1, -10).Should().Be(long.MinValue);
        NumberRange.Full<ulong>().Add(ulong.MaxValue - 1, 10).Should().Be(ulong.MaxValue);
        NumberRange.Full<int>().Add(int.MaxValue, int.MaxValue).Should().Be(int.MaxValue);
    }

    [Fact]
    public void Should_SaturateAtTheBounds_WhenMultiplying()
    {
        NumberRange.Full<byte>().Multiply(100, 3).Should().Be(255);
        NumberRange.Full<long>().Multiply(long.MaxValue, -2).Should().Be(long.MinValue);
    }

    [Fact]
    public void Should_TellWhetherAValueIsInside_WhenCheckingWithAndWithoutBounds()
    {
        var range = new NumberRange<int>(0, 10);

        range.Contains(10).Should().BeTrue();
        range.ContainsExclusive(10).Should().BeFalse();
        range.ContainsExclusive(5).Should().BeTrue();
        range.Contains(11).Should().BeFalse();
    }

    [Fact]
    public void Should_KeepDecimals_WhenTheTypeHasThem()
    {
        new NumberRange<decimal>(0m, 1m).Add(0.1m, 0.2m).Should().Be(0.3m);
        NumberRange.Full<double>().TryParse("0.25", Invariant, out var value).Should().BeTrue();
        value.Should().Be(0.25);
    }

    [Fact]
    public void Should_DescribeTheType_WhenAskedForIntegerAndSign()
    {
        NumberRange.IsInteger<byte>().Should().BeTrue();
        NumberRange.IsSigned<byte>().Should().BeFalse();
        NumberRange.IsInteger<double>().Should().BeFalse();
        NumberRange.IsSigned<double>().Should().BeTrue();
    }

    [Fact]
    public void Should_PrintInfinity_WhenABoundIsALongTypeLimit()
    {
        NumberRange.Full<int>().ToString().Should().Be("( -∞, ∞ )");
        NumberRange.Full<byte>().ToString().Should().Be("( 0, 255 )");
        new NumberRange<int>(-10, 10).ToString().Should().Be("( -10, 10 )");
    }

    [Fact]
    public void Should_Throw_WhenMinIsGreaterThanMax()
        => FluentActions.Invoking(() => new NumberRange<int>(5, 1)).Should().Throw<ArgumentException>();

    private static void CheckLimits<T>()
        where T : struct, INumber<T>, IMinMaxValue<T>
    {
        var range = NumberRange.Full<T>();
        var max = BigInteger.CreateTruncating(T.MaxValue);
        var min = BigInteger.CreateTruncating(T.MinValue);

        Parse(range, max.ToString(Invariant)).Should().Be(T.MaxValue, typeof(T).Name);
        Parse(range, min.ToString(Invariant)).Should().Be(T.MinValue, typeof(T).Name);
        Parse(range, (max + 1).ToString(Invariant)).Should().Be(T.MaxValue, typeof(T).Name);
        Parse(range, (min - 1).ToString(Invariant)).Should().Be(T.MinValue, typeof(T).Name);
    }

    private static T Parse<T>(NumberRange<T> range, string text)
        where T : struct, INumber<T>, IMinMaxValue<T>
    {
        range.TryParse(text, Invariant, out var value).Should().BeTrue(text);
        return value;
    }
}
