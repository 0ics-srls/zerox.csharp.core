using System.Collections.Concurrent;

namespace Zerox.Core.Validation;

// Named values a validation passes down to the children it validates.
public sealed class ValidationParameters
{
    private readonly ConcurrentDictionary<string, object> _values = new(StringComparer.Ordinal);

    public void Set(string name, object value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        _values[name] = value;
    }

    public TValue? Get<TValue>(string name)
        where TValue : class
        => _values.TryGetValue(name, out var value) ? value as TValue : null;
}
