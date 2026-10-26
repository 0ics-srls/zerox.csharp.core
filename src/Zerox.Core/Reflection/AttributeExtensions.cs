using System.Reflection;

namespace Zerox.Core.Reflection;

// What the framework lacks for declarative discovery: the properties marked with an attribute.
// Single lookups are the framework's GetCustomAttribute<T> / IsDefined.
public static class AttributeExtensions
{
    private const BindingFlags Properties = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;

    // Every public property carrying TAttribute (inherited markings included), with the attribute.
    public static IEnumerable<(PropertyInfo Property, TAttribute Attribute)> GetPropertiesWith<TAttribute>(this Type type)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(type);
        return Find(type);

        static IEnumerable<(PropertyInfo, TAttribute)> Find(Type type)
        {
            foreach (var property in type.GetProperties(Properties))
            {
                if (property.GetCustomAttribute<TAttribute>(inherit: true) is { } attribute)
                    yield return (property, attribute);
            }
        }
    }

    public static TAttribute? GetPropertyAttribute<TAttribute>(this Type type, string propertyName)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.GetProperty(propertyName, Properties)?.GetCustomAttribute<TAttribute>(inherit: true);
    }

    public static TAttribute? GetAttributeOfPropertyType<TAttribute>(this Type type, string propertyName)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.GetProperty(propertyName, Properties)?.PropertyType.GetCustomAttribute<TAttribute>(inherit: true);
    }
}
