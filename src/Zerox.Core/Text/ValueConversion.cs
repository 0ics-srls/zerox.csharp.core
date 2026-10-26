using System.Globalization;

namespace Zerox.Core.Text;

// Converts a value to a target type; a value that cannot be converted throws the framework's exception.
public static class ValueConversion
{
    public static object? ConvertTo(object? value, Type target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var underlying = Nullable.GetUnderlyingType(target);
        if (value is null or DBNull)
            return underlying is not null || !target.IsValueType ? null : Activator.CreateInstance(target);
        var type = underlying ?? target;
        if (type.IsInstanceOfType(value))
            return value;
        if (type.IsEnum)
            return value is string name ? Enum.Parse(type, name) : Enum.ToObject(type, value);
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    public static T? ConvertTo<T>(this object? value) => (T?)ConvertTo(value, typeof(T));
}
