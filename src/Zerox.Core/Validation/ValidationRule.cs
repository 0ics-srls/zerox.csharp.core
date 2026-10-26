namespace Zerox.Core.Validation;

// A rule that writes its own message: false with the error when the subject breaks it.
public delegate bool RuleWithError<in T>(T subject, out object? error);

// One rule on one property ("" = the whole object).
public sealed class ValidationRule<T>
{
    private readonly Func<T, bool>? _check;
    private readonly RuleWithError<T>? _checkWithError;
    private readonly object? _error;

    public ValidationRule(string propertyName, object error, Func<T, bool> check)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(check);
        PropertyName = propertyName;
        _error = error;
        _check = check;
    }

    public ValidationRule(string propertyName, RuleWithError<T> check)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        ArgumentNullException.ThrowIfNull(check);
        PropertyName = propertyName;
        _checkWithError = check;
    }

    public string PropertyName { get; }

    // The error when the subject breaks the rule, null when it keeps it.
    public object? Apply(T subject)
    {
        if (_check is not null)
            return _check(subject) ? null : _error;
        if (_checkWithError!(subject, out var error))
            return null;
        return error ?? throw new InvalidOperationException($"A rule of {PropertyName} failed without an error");
    }
}
