namespace Zerox.Core.Validation;

// Anything a validation walks into.
public interface IValidatable
{
    // True when there is no error; the parameters come from the parent (null: keep the own ones).
    bool Validate(ValidationParameters? parameters = null);

    IReadOnlyList<object> GetAllErrors();
}
