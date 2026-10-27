using System.Reflection;
using System.Text.Json.Serialization;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain;

// How a path of the load options ("Box.Label") walks the properties of an entity.
internal static class EntityPaths
{
    internal const BindingFlags PublicProperties = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

    // The chain of properties of a path, case-insensitive like DevExtreme; null if a segment is missing.
    // On a collection the type goes on with the type of the element.
    internal static IReadOnlyList<PropertyInfo>? Resolve(Type type, string path)
    {
        var chain = new List<PropertyInfo>();
        foreach (var segment in path.Split('.'))
        {
            var property = type.GetProperty(segment, PublicProperties);
            if (property is null)
                return null;
            chain.Add(property);
            type = Inner(property.PropertyType);
        }
        return chain;
    }

    // A navigation: a property whose type (or element type) is an entity.
    internal static bool IsNavigation(Type type) => typeof(IEntity).IsAssignableFrom(Inner(type));

    // The type a path goes on with: the element for a collection, else the type itself.
    internal static Type Inner(Type type)
    {
        if (type == typeof(string))
            return type;
        var element = type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return element?.GetGenericArguments()[0] ?? type;
    }

    // A reserved field: [JsonIgnore] and not a navigation. A route with load options is full access to
    // the table, so such a field is never named in a search and never listed.
    internal static bool IsReserved(PropertyInfo property) =>
        property.IsDefined(typeof(JsonIgnoreAttribute)) && !IsNavigation(property.PropertyType);
}
