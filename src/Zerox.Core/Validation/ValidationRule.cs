namespace Zerox.Core.Validation;

// A rule that writes its own message: false with the error when the subject breaks it.
public delegate bool RuleWithError<in T>(T subject, out object? error);

// One rule on one property ("" = the whole object).
public sealed class ValidationRule<T>
{
    // Both forms of rule as one: a rule with a fixed error writes that error when the subject breaks it.
    private readonly RuleWithError<T> _check;

    public ValidationRule(string propertyName, object error, Func<T, bool> check)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(check);
        PropertyName = propertyName;
        _check = (T subject, out object? found) =>
        {
            found = check(subject) ? null : error;
            return found is null;
        };
    }

    public ValidationRule(string propertyName, RuleWithError<T> check)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        ArgumentNullException.ThrowIfNull(check);
        PropertyName = propertyName;
        _check = check;
    }

    public string PropertyName { get; }

    // The error when the subject breaks the rule, null when it keeps it.
    public object? Apply(T subject)
    {
        if (_check(subject, out var error))
            return null;
        return error ?? throw new InvalidOperationException($"A rule of {PropertyName} failed without an error");
    }
}
