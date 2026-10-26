using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Zerox.Core.ComponentModel;

namespace Zerox.Core.Avalonia.ComponentModel;

// The view model base: MemberChanged at once on the caller's thread (for the model), PropertyChanged on
// the UI thread through the Dispatcher (for the bindings), optionally throttled and debounced.
public abstract class PropertyContainer : NotifyingObject, IDisposable
{
    private const string EveryProperty = "";
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, PendingChange> _pending = new(StringComparer.Ordinal);
    private readonly Lock _nestedLock = new();
    private readonly Dictionary<object, Action> _nested = new(ReferenceEqualityComparer.Instance);
    private ThrottlePump? _pump;
    private int _queued;
    private volatile bool _eventsOn = true;
    private volatile bool _disposed;

    protected PropertyContainer(TimeProvider? timeProvider = null) => _time = timeProvider ?? TimeProvider.System;

    // At once, on the thread that changed the property.
    public event PropertyChangedEventHandler? MemberChanged;

    public bool IsEventThrottlingActive => _pump is not null;

    public TimeSpan ThrottleInterval { get; private set; }

    public TimeSpan DebounceInterval { get; private set; }

    public void ActivateEventThrottling(TimeSpan interval, TimeSpan debounce = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(debounce, TimeSpan.Zero);
        ThrottleInterval = interval;
        DebounceInterval = debounce;
        _pump = ThrottlePump.For(_time, interval);
    }

    // Back to posted events; what was waiting is sent now.
    public void StopEventThrottling()
    {
        _pump = null;
        foreach (var (name, change) in _pending)
        {
            if (change.HasUnsent)
            {
                change.Commit();
                Post(name);
            }
        }
    }

    public void EventsOff() => _eventsOn = false;

    public void EventsOn() => _eventsOn = true;

    public void EventsOnAndNotifyAll()
    {
        _eventsOn = true;
        NotifyPropertyChanged(propertyName: null);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // Called by the pump on the UI thread.
    internal void Flush()
    {
        Interlocked.Exchange(ref _queued, 0);
        if (_disposed || _pump is not { } pump)
            return;
        var waiting = false;
        foreach (var (name, change) in _pending)
        {
            if (change.IsDue(_time, DebounceInterval))
            {
                change.Commit();
                Raise(name);
            }
            else if (change.HasUnsent)
            {
                waiting = true;
            }
        }
        if (waiting)
            AskThePump(pump);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;
        _pump = null;
        if (!disposing)
            return;
        lock (_nestedLock)
        {
            foreach (var stopFollowing in _nested.Values)
                stopFollowing();
            _nested.Clear();
        }
    }

    // A value that notifies its own changes: a change inside it is a change of this property; the previous value is let go.
    protected bool SetComplexProperty<TValue>(ref TValue? field, TValue? value, [CallerMemberName] string? propertyName = null)
        where TValue : class, INotifyPropertyChanged
    {
        if (ReferenceEquals(field, value))
            return false;
        lock (_nestedLock)
        {
            if (field is not null && _nested.Remove(field, out var stopFollowing))
                stopFollowing();
            if (value is not null)
                _nested[value] = Follow(value, propertyName);
        }
        field = value;
        NotifyPropertyChanged(propertyName);
        return true;
    }

    // AVA2001 is for AvaloniaObject.OnPropertyChanged; this is NotifyingObject's seam, and not calling
    // the base at once is the point: the event is posted or throttled, and Raise calls the base later.
#pragma warning disable AVA2001
    protected override void OnPropertyChanged(string? propertyName)
#pragma warning restore AVA2001
    {
        if (!_eventsOn || _disposed)
            return;
        MemberChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (_pump is { } pump)
        {
            _pending.GetOrAdd(propertyName ?? EveryProperty, static _ => new PendingChange()).Mark(_time.GetTimestamp());
            AskThePump(pump);
        }
        else
        {
            Post(propertyName ?? EveryProperty);
        }
    }

    private void AskThePump(ThrottlePump pump)
    {
        if (Interlocked.Exchange(ref _queued, 1) == 0)
            pump.Enqueue(this);
    }

    // A container is followed through MemberChanged (at once), any other value through PropertyChanged.
    private Action Follow(INotifyPropertyChanged value, string? propertyName)
    {
        PropertyChangedEventHandler handler = (_, _) => NotifyPropertyChanged(propertyName);
        if (value is PropertyContainer container)
        {
            container.MemberChanged += handler;
            return () => container.MemberChanged -= handler;
        }
        value.PropertyChanged += handler;
        return () => value.PropertyChanged -= handler;
    }

    private void Post(string name) => Dispatcher.UIThread.Post(() => Raise(name));

    private void Raise(string name)
    {
        if (!_disposed)
            base.OnPropertyChanged(name.Length == 0 ? null : name);
    }
}
