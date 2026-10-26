using Zerox.Core.Collections;

namespace Zerox.Core.Tests.Collections;

public sealed class BitMaskTests
{
    private readonly BitMask _a = Mask(100, 10, 20, 30, 40, 50);
    private readonly BitMask _b = Mask(100, 10, 30, 50, 70, 90);

    [Fact]
    public void Test_Count()
    {
        Assert.Equal(5, _a.Count);
        Assert.Equal(5, _b.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => _a.Set(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => _a.Set(-1));
    }

    [Fact]
    public void Test_Get()
    {
        Assert.True(_a.Get(10) && _a.Get(20) && _a.Get(30) && _a.Get(40) && _a.Get(50));
        Assert.False(_a.Get(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _a.Get(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => _a.Get(-1));
    }

    [Fact]
    public void Test_Reset()
    {
        foreach (var bit in new[] { 10, 20, 30, 40, 50 })
            _a.Reset(bit);

        Assert.Equal(0, _a.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => _a.Reset(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => _a.Reset(-1));
    }

    [Fact]
    public void Test_Logic()
    {
        Assert.Equal(7, (_a | _b).Count);
        Assert.Equal(3, (_a & _b).Count);
        var same = _a;
        Assert.True(_a == same);
        Assert.True(_a != _b);
    }

    [Fact]
    public void Test_IndexList()
        => Assert.Equal([10, 20, 30, 40, 50], _a.GetSetBits());

    [Fact]
    public void Should_LeaveTheOperandsUnchanged_WhenCompared()
    {
        _ = _a == _b;
        _ = _a | _b;
        _ = _a & _b;

        _a.GetSetBits().Should().Equal(10, 20, 30, 40, 50);
        _b.GetSetBits().Should().Equal(10, 30, 50, 70, 90);
    }

    [Fact]
    public void Should_BeEqual_WhenTwoMasksHoldTheSameBits()
    {
        var copy = Mask(100, 10, 20, 30, 40, 50);

        (_a == copy).Should().BeTrue();
        _a.Equals(copy).Should().BeTrue();
        _a.GetHashCode().Should().Be(copy.GetHashCode());
        (Mask(101, 10, 20, 30, 40, 50) == _a).Should().BeFalse();
    }

    [Fact]
    public void Should_CountAcrossWords_WhenTheMaskIsLongerThan64Bits()
    {
        var mask = Mask(200, 0, 63, 64, 199);

        mask.Count.Should().Be(4);
        mask.GetSetBits().Should().Equal(0, 63, 64, 199);
    }

    [Fact]
    public void Should_Throw_WhenCombiningMasksOfDifferentSizes()
    {
        FluentActions.Invoking(() => _a | new BitMask(64)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => _a & new BitMask(64)).Should().Throw<ArgumentException>();
    }

    private static BitMask Mask(int size, params int[] bits)
    {
        var mask = new BitMask(size);
        foreach (var bit in bits)
            mask.Set(bit);
        return mask;
    }
}
