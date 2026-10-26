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
