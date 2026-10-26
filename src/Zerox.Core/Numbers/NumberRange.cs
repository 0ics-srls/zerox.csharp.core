using System.Globalization;
using System.Numerics;

namespace Zerox.Core.Numbers;

// Factories and type facts of NumberRange<T> (static members live on a non-generic class, CA1000).
public static class NumberRange
{
    public static NumberRange<T> Full<T>()
        where T : struct, INumber<T>, IMinMaxValue<T>
        => new(T.MinValue, T.MaxValue);

    // A missing bound is the type's limit.
    public static NumberRange<T> Of<T>(T? min, T? max)
        where T : struct, INumber<T>, IMinMaxValue<T>
        => new(min ?? T.MinValue, max ?? T.MaxValue);

    public static bool IsInteger<T>()
        where T : struct, INumber<T>
        => IntegerTypes<T>.Value;

    public static bool IsSigned<T>()
        where T : struct, INumber<T>, IMinMaxValue<T>
        => T.IsNegative(T.MinValue);

    private static class IntegerTypes<T>
    {
        public static readonly bool Value = typeof(T).GetInterfaces()
            .Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IBinaryInteger<>));
    }
}

// An interval of a numeric type: clamp, contain, step and parse inside it. The bounds default to the type's limits.
public readonly record struct NumberRange<T>
    where T : struct, INumber<T>, IMinMaxValue<T>
{
    public NumberRange(T min, T max)
    {
        if (min > max)
            throw new ArgumentException($"Min {min} is greater than max {max}", nameof(min));
        Min = min;
        Max = max;
    }

    public T Min { get; }

    public T Max { get; }

    public T Clamp(T value) => T.Clamp(value, Min, Max);

    public bool Contains(T value) => value >= Min && value <= Max;

    public bool ContainsExclusive(T value) => value > Min && value < Max;

    // value + step, stopping at the bounds
    public T Add(T value, T step) => Saturate(() => checked(value + step), T.IsNegative(step));

    // value * factor, stopping at the bounds
    public T Multiply(T value, T factor) => Saturate(() => checked(value * factor), T.IsNegative(value) != T.IsNegative(factor));

    // Text into the interval: beyond the interval (or the type) it becomes the nearest bound; not a number gives false.
    public bool TryParse(string? text, IFormatProvider? provider, out T value)
    {
        var integer = NumberRange.IsInteger<T>();
        var style = integer ? NumberStyles.Integer : NumberStyles.Float | NumberStyles.AllowThousands;
        if (T.TryParse(text, style, provider, out var parsed))
        {
            value = Clamp(parsed);
            return true;
        }
        if (integer && BigInteger.TryParse(text, NumberStyles.Integer, provider, out var big))
        {
            value = Clamp(T.CreateSaturating(big));
            return true;
        }
        if (!integer && double.TryParse(text, style, provider, out var real))
        {
            value = Clamp(T.CreateSaturating(real));
            return true;
        }
        value = default;
        return false;
    }

    public override string ToString()
        => $"( {Bound(Min, T.MinValue, "-∞")}, {Bound(Max, T.MaxValue, "∞")} )";

    // A bound equal to the type's limit with a long text prints as infinity.
    private static string Bound(T bound, T limit, string infinity)
    {
        var text = bound.ToString(null, CultureInfo.InvariantCulture);
        return bound == limit && text.Length > 5 ? infinity : text;
    }

    // An overflow is the expected outcome of saturation: it is handled right here and becomes the bound.
    private T Saturate(Func<T> operation, bool towardsMin)
    {
        try
        {
            return Clamp(operation());
        }
        catch (OverflowException)
        {
            return towardsMin ? Min : Max;
        }
    }
}
