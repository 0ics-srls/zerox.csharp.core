using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Zerox.Core.Reflection;

// The cached accessors (static members live on a non-generic class, CA1000).
public static class FastProperty
{
    // The accessor of a property, compiled once per (property, publicOnly) and reused.
    public static FastProperty<TInstance, TValue> For<TInstance, TValue>(PropertyInfo property, bool publicOnly = true)
    {
        ArgumentNullException.ThrowIfNull(property);
        return Cache<TInstance, TValue>.Items.GetOrAdd((property, publicOnly), static key => new FastProperty<TInstance, TValue>(key.Item1, key.Item2));
    }

    private static class Cache<TInstance, TValue>
    {
        public static readonly ConcurrentDictionary<(PropertyInfo, bool), FastProperty<TInstance, TValue>> Items = new();
    }
}

// Getter and setter of a property compiled once; calls are delegates, not reflection.
public sealed class FastProperty<TInstance, TValue>
{
    private readonly Func<TInstance, TValue>? _get;
    private readonly Action<TInstance, TValue>? _set;

    internal FastProperty(PropertyInfo property, bool publicOnly)
    {
        Property = property;
        (_get, _set) = Compile(property, publicOnly);
    }

    public PropertyInfo Property { get; }

    public bool CanRead => _get is not null;

    public bool CanWrite => _set is not null;

    public TValue Get(TInstance instance)
        => (_get ?? throw new InvalidOperationException($"{Property.DeclaringType?.Name}.{Property.Name} has no readable getter")).Invoke(instance);

    public void Set(TInstance instance, TValue value)
        => (_set ?? throw new InvalidOperationException($"{Property.DeclaringType?.Name}.{Property.Name} has no writable setter")).Invoke(instance, value);

    private static (Func<TInstance, TValue>?, Action<TInstance, TValue>?) Compile(PropertyInfo property, bool publicOnly)
    {
        var declaring = property.DeclaringType ?? throw new ArgumentException($"{property.Name} has no declaring type", nameof(property));
        var instance = Expression.Parameter(typeof(TInstance), "instance");
        var value = Expression.Parameter(typeof(TValue), "value");
        Expression target = typeof(TInstance) == declaring ? instance
            : declaring.IsValueType ? Expression.Convert(instance, declaring)
            : Expression.TypeAs(instance, declaring);

        Func<TInstance, TValue>? get = null;
        if (property.GetGetMethod(nonPublic: !publicOnly) is { } getter)
        {
            Expression read = Expression.Call(target, getter);
            if (typeof(TValue) != property.PropertyType)
                read = Expression.Convert(read, typeof(TValue));
            get = Expression.Lambda<Func<TInstance, TValue>>(read, instance).Compile();
        }

        Action<TInstance, TValue>? set = null;
        if (property.GetSetMethod(nonPublic: !publicOnly) is { } setter)
        {
            Expression written = typeof(TValue) == property.PropertyType ? value
                : property.PropertyType.IsValueType ? Expression.Convert(value, property.PropertyType)
                : Expression.TypeAs(value, property.PropertyType);
            set = Expression.Lambda<Action<TInstance, TValue>>(Expression.Call(target, setter, written), instance, value).Compile();
        }

        return (get, set);
    }
}
