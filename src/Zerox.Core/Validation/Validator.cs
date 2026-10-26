using System.Collections;
using System.ComponentModel;
using System.Reflection;

namespace Zerox.Core.Validation;

// The errors of one object: its rules per property, then its validatable children. A component the
// object holds; the object forwards INotifyDataErrorInfo to it.
public sealed class Validator<T> : INotifyDataErrorInfo, IValidatable
    where T : class
{
    private readonly Lock _lock = new();
    private readonly T _subject;
    private readonly ValidationRules<T> _rules;
    private readonly PropertyInfo[] _children;
    private readonly Dictionary<string, IReadOnlyList<object>> _errors = new(StringComparer.Ordinal);

    public Validator(T subject, ValidationRules<T> rules)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(rules);
        _subject = subject;
        _rules = rules;
        _children = [.. subject.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(IsChild)];
    }

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public ValidationParameters Parameters { get; private set; } = new();

    public bool HasErrors
    {
        get
        {
            lock (_lock)
                return _errors.Count > 0;
        }
    }

    public bool Validate(ValidationParameters? parameters = null)
    {
        if (parameters is not null)
            Parameters = parameters;
        var changed = new List<string>();
        foreach (var propertyName in _rules.Select(rule => rule.PropertyName).Distinct(StringComparer.Ordinal))
            Store(propertyName, _rules.Apply(_subject, propertyName), changed);
        foreach (var child in _children)
            Store(child.Name, ValidateChild(child.GetValue(_subject)), changed);
        Raise(changed);
        return !HasErrors;
    }

    // Re-applies the rules of one property (after it changed); true when it has no error.
    public bool Validate(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        var changed = new List<string>();
        var errors = _rules.Apply(_subject, propertyName);
        Store(propertyName, errors, changed);
        Raise(changed);
        return errors.Count == 0;
    }

    // Null or "": every error.
    public IReadOnlyList<object> GetErrors(string? propertyName)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(propertyName))
                return [.. _errors.Values.SelectMany(errors => errors)];
            return _errors.TryGetValue(propertyName, out var errors) ? errors : [];
        }
    }

    public IReadOnlyList<object> GetAllErrors() => GetErrors(null);

    IEnumerable INotifyDataErrorInfo.GetErrors(string? propertyName) => GetErrors(propertyName);

    private List<object> ValidateChild(object? value)
    {
        IEnumerable<IValidatable> children = value switch
        {
            IValidatable one => [one],
            IEnumerable many => many.OfType<IValidatable>(),
            _ => [],
        };
        var errors = new List<object>();
        foreach (var child in children)
        {
            child.Validate(Parameters);
            errors.AddRange(child.GetAllErrors());
        }
        return errors;
    }

    private void Store(string propertyName, IReadOnlyList<object> errors, List<string> changed)
    {
        lock (_lock)
        {
            var had = _errors.TryGetValue(propertyName, out var previous);
            if (errors.Count == 0)
            {
                if (_errors.Remove(propertyName))
                    changed.Add(propertyName);
            }
            else if (!had || !previous!.SequenceEqual(errors))
            {
                _errors[propertyName] = errors;
                changed.Add(propertyName);
            }
        }
    }

    private void Raise(List<string> changed)
    {
        foreach (var propertyName in changed)
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    private static bool IsChild(PropertyInfo property)
        => property.GetIndexParameters().Length == 0
           && (typeof(IValidatable).IsAssignableFrom(property.PropertyType)
               || property.PropertyType.GetInterfaces().Append(property.PropertyType)
                   .Any(type => type.IsGenericType
                                && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                                && typeof(IValidatable).IsAssignableFrom(type.GetGenericArguments()[0])));
}
