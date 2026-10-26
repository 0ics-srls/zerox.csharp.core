using System.Collections.ObjectModel;

namespace Zerox.Core.Validation;

// The rules of a type.
public sealed class ValidationRules<T> : Collection<ValidationRule<T>>
{
    public void Add(string propertyName, object error, Func<T, bool> check) => Add(new ValidationRule<T>(propertyName, error, check));

    public void Add(string propertyName, RuleWithError<T> check) => Add(new ValidationRule<T>(propertyName, check));

    // The errors in rule order: null = every rule, "" = the whole-object rules, a name = that property's rules.
    public IReadOnlyList<object> Apply(T subject, string? propertyName = null)
        => [.. this.Where(rule => propertyName is null || rule.PropertyName == propertyName)
                   .Select(rule => rule.Apply(subject))
                   .OfType<object>()];
}
