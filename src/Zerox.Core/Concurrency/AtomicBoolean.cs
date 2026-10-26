namespace Zerox.Core.Concurrency;

// A boolean changed atomically; the callback runs only when the value really changes.
public sealed class AtomicBoolean(bool initialValue = false, Action? changed = null)
{
    private int _value = initialValue ? 1 : 0;

    public bool Value
    {
        get => Volatile.Read(ref _value) == 1;
        set
        {
            var next = value ? 1 : 0;
            if (Interlocked.Exchange(ref _value, next) != next)
                changed?.Invoke();
        }
    }

    // Changes false to true; true when this call made the change.
    public bool FalseToTrue() => SetWhen(true, false);

    // Changes true to false; true when this call made the change.
    public bool TrueToFalse() => SetWhen(false, true);

    // Sets setTo when the value is when; true when the value was when.
    public bool SetWhen(bool setTo, bool when)
    {
        int expected = when ? 1 : 0, next = setTo ? 1 : 0;
        var matched = Interlocked.CompareExchange(ref _value, next, expected) == expected;
        if (matched && next != expected)
            changed?.Invoke();
        return matched;
    }
}
