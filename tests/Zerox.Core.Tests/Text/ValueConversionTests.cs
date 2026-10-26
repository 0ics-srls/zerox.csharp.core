using System.Globalization;
using Zerox.Core.Text;

namespace Zerox.Core.Tests.Text;

public sealed class ValueConversionTests
{
    public enum SomeKind
    {
        Null = 0,
        A = 1,
        B = 8,
        C = 16,
    }

    [Theory]
    [InlineData((byte)0x7f, 0x7fL)]
    [InlineData((short)0x7fff, 0x7fffL)]
    [InlineData((ushort)0xffff, 0xffffL)]
    [InlineData((long)0x7fffffff, 0x7fffffffL)]
    [InlineData((ulong)0xffffffff, 0xffffffffL)]
    [InlineData(0x7fffffff, 0x7fffffffL)]
    [InlineData(SomeKind.C, 16L)]
    public void Test_Cast_to_int(object what, long result)
        => Assert.Equal(result, what.ConvertTo<long>());

    [Fact]
    public void Test_Cast_to_Enum()
        => Assert.Equal(SomeKind.C, 16.ConvertTo<SomeKind>());

    [Fact]
    public void Test_Cast_with_null()
        => Assert.Equal(0, ((object?)null).ConvertTo<int>());

    [Fact]
    public void Should_ReturnNull_WhenTheTargetIsNullableAndTheValueIsNull()
    {
        // the Type overload is the one under test (a property type known only at run time)
        Type nullableInt = typeof(int?), text = typeof(string);

        ValueConversion.ConvertTo(null, nullableInt).Should().BeNull();
        ValueConversion.ConvertTo(DBNull.Value, text).Should().BeNull();
    }

    [Fact]
    public void Should_ConvertTheUnderlyingType_WhenTheTargetIsNullable()
        => "42".ConvertTo<int?>().Should().Be(42);

    [Fact]
    public void Should_ReadTheEnumByName_WhenTheValueIsText()
    {
        "B".ConvertTo<SomeKind>().Should().Be(SomeKind.B);
        FluentActions.Invoking(() => "Nope".ConvertTo<SomeKind>()).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_UseTheInvariantCulture_WhenConvertingText()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("it-IT");

            "1.5".ConvertTo<double>().Should().Be(1.5);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Should_LetTheFrameworkExceptionReachTheCaller_WhenTheValueCannotBeConverted()
    {
        FluentActions.Invoking(() => "abc".ConvertTo<int>()).Should().Throw<FormatException>();
        FluentActions.Invoking(() => 300.ConvertTo<byte>()).Should().Throw<OverflowException>();
    }

    [Fact]
    public void Should_ReturnTheSameInstance_WhenTheValueIsAlreadyOfTheTarget()
    {
        var list = new List<int> { 1, 2 };
        Type target = typeof(IEnumerable<int>);

        ValueConversion.ConvertTo(list, target).Should().BeSameAs(list);
    }
}
