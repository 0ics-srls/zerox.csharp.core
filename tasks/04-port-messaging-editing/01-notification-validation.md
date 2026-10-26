# Notification and validation

<block id="01-notifying-object">
## TDDAB-1: Write the One Change Notification

<intro>
Delivers `NotifyingObject` (abstract, `INotifyPropertyChanged`), the one change notification of
the library (user decision: a minimal base in `Zerox.Core`; stage D extends it with the throttle
pump and the UI thread):
- `SetProperty<TValue>(ref TValue field, TValue value, [CallerMemberName] string? propertyName = null)`
  (protected, virtual): sets the field and notifies only when the value really changed
  (`EqualityComparer<TValue>.Default`); returns whether it changed;
- `NotifyPropertyChanged([CallerMemberName] string? propertyName = null)` and
  `NotifyPropertiesChanged(params ReadOnlySpan<string> propertyNames)` (protected); a null name
  means "every property" (the convention of WPF and Avalonia bindings);
- `OnPropertyChanged(string? propertyName)` (protected, virtual): every notification passes here,
  so a subclass can defer or move it (stage D's pump); the default raises `PropertyChanged` at once;
- an exception of a handler reaches the code that set the property (P9).

Capability: old `../../tmp/zerox/Zerox.Wpf.Core/Core/Containers/PropertyContainer.cs` and
`MemberContainer.cs` (dispatcher, throttle pump, `InvalidateCommands`, events on/off): only the
notification core comes here; the rest is stage D.

Files: `src/Zerox.Core/ComponentModel/NotifyingObject.cs`; tests
`tests/Zerox.Core.Tests/ComponentModel/NotifyingObjectTests.cs`.
</intro>

<red>
- test: Should_NotifyOnce_WhenTheValueChanges — setting `Name` from "" to "a": `SetProperty` returns true, one `PropertyChanged` with name "Name", the field is "a"
- test: Should_NotNotify_WhenTheValueIsTheSame — setting `Name` to its current value returns false and raises nothing
- test: Should_NotifyEveryName_WhenSeveralAreGiven — `NotifyPropertiesChanged("A", "B")` raises "A" then "B"
- test: Should_NotifyEveryProperty_WhenTheNameIsNull — `NotifyPropertyChanged(null)` raises one event whose `PropertyName` is null
- test: Should_PassEveryNotificationThroughOnPropertyChanged_WhenASubclassOverridesIt — a subclass recording `OnPropertyChanged` and not calling the base sees "Name" and raises nothing
- test: Should_LetTheHandlerExceptionReachTheSetter_WhenAHandlerThrows — a handler throwing `InvalidOperationException`: the property setter throws it
</red>

### Implementation

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Zerox.Core.ComponentModel;

// The one change notification of the library: no throttle, no thread (stage D adds them by
// overriding OnPropertyChanged).
public abstract class NotifyingObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // Sets the field and notifies when the value really changed; returns whether it changed.
    protected virtual bool SetProperty<TValue>(ref TValue field, TValue value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<TValue>.Default.Equals(field, value))
            return false;
        field = value;
        NotifyPropertyChanged(propertyName);
        return true;
    }

    // A null name means every property.
    protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = null) => OnPropertyChanged(propertyName);

    protected void NotifyPropertiesChanged(params ReadOnlySpan<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
            OnPropertyChanged(propertyName);
    }

    // Every notification passes here.
    protected virtual void OnPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

<success>
- [ ] `NotifyingObjectTests` pass (6 tests)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="02-validation">
## TDDAB-2: Port the Validation Rules and the One Validator

<intro>
Delivers, in `src/Zerox.Core/Validation`:
- `RuleWithError<in T>`: delegate `bool (T subject, out object? error)` (a rule that writes its own message);
- `ValidationRule<T>` (sealed): `PropertyName` ("" = a rule of the whole object), built from
  `(propertyName, error, Func<T, bool> check)` or `(propertyName, RuleWithError<T> check)`;
  `Apply(subject)` returns the error when the subject breaks the rule, null when it keeps it (a
  `RuleWithError` that fails without an error → `InvalidOperationException`);
- `ValidationRules<T>` (sealed, `Collection<ValidationRule<T>>`, collection initializer):
  `Add(propertyName, error, check)`, `Add(propertyName, RuleWithError<T>)`,
  `Apply(subject, propertyName = null)` → the errors in rule order (null = every rule, "" = the
  whole-object rules, a name = that property's rules);
- `ValidationParameters` (sealed): named values passed down to children (`Set(name, value)`,
  `Get<TValue>(name)` → null when absent or of another type);
- `IValidatable`: `bool Validate(ValidationParameters? parameters = null)`, `IReadOnlyList<object> GetAllErrors()`;
- `Validator<T>` (sealed, `T : class`, `INotifyDataErrorInfo`, `IValidatable`): a component the
  validated object holds (composition, no base class). `Validator(T subject, ValidationRules<T> rules)`;
  `Validate(parameters?)` applies every rule per property name, then validates the CHILDREN — the
  public properties of the subject's runtime type whose type is `IValidatable` or an
  `IEnumerable<X>` with `X : IValidatable` — passing the parameters down, the errors of a child
  property kept under that property's name; returns true when there is no error.
  `Validate(string propertyName)` re-applies only that property's rules (after it changed).
  `GetErrors(string? propertyName)` (null or "" = all errors), `GetAllErrors()`, `HasErrors`,
  `Parameters`; `ErrorsChanged` is raised once for each property whose errors changed, after the
  validation, outside the lock. Thread safe (one `Lock`);
- `ValidationException` (sealed): `Errors` (`IReadOnlyList<object>`), message = the errors one per
  line; the standard constructors.

Capability: old `IRule`, `Rule<T>`, `DelegateRule<T>`, `IRuleCollection`, `RuleCollection<T>`,
`IValidable`, `IValidableParams`, `ValidableBase<T>`, `ValidationException` in
`../../tmp/zerox/Zerox.Wpf.Core/Core/Containers/Validation/`, plus a second copy of the error map
in `../../tmp/zerox/Zerox.Wpf.Core/Core/Wpf/Popup/DialogPopupContentValidableBaseVM.cs`; test
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Wpf/Popup/PropertyValidationTest.cs` (`Test_Valid`,
`Test_NoRules`). ONE error map (`Validator<T>`) instead of two; the old extended rule wrote its
message into a field shared by every call (now returned); the old `Test_NoRules` asserted on the
text of a `List` (`string.Join` of one object): ported as "no errors". In the ported tests the
dialog model becomes a `NotifyingObject` (block 01) that holds a `Validator`, forwards
`INotifyDataErrorInfo` to it and re-validates a property in `OnPropertyChanged`.

Files: `src/Zerox.Core/Validation/ValidationRule.cs` (delegate + rule), `ValidationRules.cs`,
`ValidationParameters.cs`, `IValidatable.cs`, `Validator.cs`, `ValidationException.cs`; tests
`tests/Zerox.Core.Tests/Validation/ValidatorTests.cs`, `tests/Zerox.Core.Tests/Validation/ValidationRuleTests.cs`.
</intro>

<red>
- test: Test_Valid — model with three rules on `StrProperty` (not empty; length 5–10 unless null; starts with "B" unless null, written with `RuleWithError`): `Validate()` false, `ErrorsChanged` raised, `HasErrors`, errors joined by " " = "StrProperty cannot be empty. StrProperty must be {5, 10} len. StrProperty must must start with B."; `StrProperty = "aaa"` → the last two; `"aaaaaa"` → only the "B" one; `"Baaaaaa"` → no error and `HasErrors` false
- test: Test_NoRules — model without rules: `HasErrors` false, `GetErrors("StrProperty")` empty, `Validate()` true, no `ErrorsChanged`
- test: Should_ReturnTheErrorOrNull_WhenARuleIsApplied — a `(property, error, check)` rule returns the error for a breaking subject and null for a keeping one; a `RuleWithError` rule returns its own message
- test: Should_Throw_WhenARuleWithErrorFailsWithoutAnError — `RuleWithError` returning false with a null error: `Apply` throws `InvalidOperationException`
- test: Should_ApplyOnlyTheRulesOfTheProperty_WhenANameIsGiven — rules on "A", "B" and "" (whole object): `Apply(s, "A")` only A's, `Apply(s, "")` only the whole-object one, `Apply(s)` all three in rule order
- test: Should_ValidateTheChildren_WhenPropertiesAreValidatable — a parent with a child `IValidatable` property and a `List` of `IValidatable`: their errors are under the two property names; fixing them and validating again removes them
- test: Should_PassTheParametersDown_WhenChildrenAreValidated — a parameter "limit" set on the parent's `Validate(parameters)`: the child's `Validate` receives the same `ValidationParameters` and reads it with `Get<string>`
- test: Should_RaiseErrorsChangedOnce_WhenAPropertyChangesItsErrors — validating twice with the same errors raises `ErrorsChanged` once for that property; `Validate("A")` with A fixed raises it for "A" only
- test: Should_ListTheErrorsOneByLine_WhenAValidationExceptionIsBuilt — `new ValidationException(["a", "b"])`: `Errors` `["a", "b"]`, message "a" + newline + "b"
</red>

### Implementation

`ValidationRule.cs`:

```csharp
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
```

`ValidationRules.cs`:

```csharp
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
```

`ValidationParameters.cs`:

```csharp
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
```

`IValidatable.cs`:

```csharp
namespace Zerox.Core.Validation;

// Anything a validation walks into.
public interface IValidatable
{
    // True when there is no error; the parameters come from the parent (null: keep the own ones).
    bool Validate(ValidationParameters? parameters = null);

    IReadOnlyList<object> GetAllErrors();
}
```

`Validator.cs`:

```csharp
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

    private IReadOnlyList<object> ValidateChild(object? value)
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
```

`ValidationException.cs`:

```csharp
namespace Zerox.Core.Validation;

// Validation errors that reach a caller as an exception.
public sealed class ValidationException : Exception
{
    public ValidationException()
    {
    }

    public ValidationException(string message)
        : base(message)
    {
    }

    public ValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ValidationException(IReadOnlyList<object> errors)
        : base(string.Join(Environment.NewLine, errors ?? throw new ArgumentNullException(nameof(errors))))
        => Errors = errors;

    public IReadOnlyList<object> Errors { get; } = [];
}
```

Test model shape (`ValidatorTests.cs`):

```csharp
public sealed class TestModel : NotifyingObject, INotifyDataErrorInfo
{
    private readonly Validator<TestModel> _validator;
    private string _strProperty = string.Empty;

    public TestModel()
    {
        _validator = new(this, new ValidationRules<TestModel>
        {
            { nameof(StrProperty), "StrProperty cannot be empty.", x => !string.IsNullOrEmpty(x.StrProperty) },
            { nameof(StrProperty), "StrProperty must be {5, 10} len.", x => x.StrProperty == default || x.StrProperty.Length is >= 5 and <= 10 },
            { nameof(StrProperty), (TestModel x, out object? err) => { err = "StrProperty must must start with B."; return x.StrProperty == default || x.StrProperty.StartsWith('B'); } },
        });
        _validator.ErrorsChanged += (_, e) => ErrorsChanged?.Invoke(this, e);
    }

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public string StrProperty
    {
        get => _strProperty;
        set => SetProperty(ref _strProperty, value);
    }

    public bool HasErrors => _validator.HasErrors;

    public bool Validate() => _validator.Validate();

    public IEnumerable GetErrors(string? propertyName) => _validator.GetErrors(propertyName);

    protected override void OnPropertyChanged(string? propertyName)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is not null)
            _validator.Validate(propertyName);
    }
}
```

<success>
- [ ] `ValidatorTests` and `ValidationRuleTests` pass (9 tests)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
