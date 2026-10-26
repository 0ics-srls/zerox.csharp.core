# Component model

<block id="01-property-container">
## TDDAB-1: Port the Property Container and Its Throttle Pump

<intro>
Delivers `PropertyContainer` (abstract, `src/Zerox.Core.Avalonia/ComponentModel/PropertyContainer.cs`),
the view model base, deriving from stage C's `NotifyingObject` and overriding its
`OnPropertyChanged` (every notification passes there):
- `MemberChanged` (`PropertyChangedEventHandler`): raised AT ONCE on the thread that changed the
  property — for listeners in the model;
- `PropertyChanged` (inherited): raised ON THE UI THREAD, posted with `Dispatcher.UIThread.Post` —
  for the bindings; a null name means every property;
- throttling: `ActivateEventThrottling(TimeSpan interval, TimeSpan debounce = default)` (interval
  ≤ 0 or debounce < 0 → `ArgumentOutOfRangeException`), `StopEventThrottling()` (back to posted
  events; what was waiting is sent now — the old code dropped it), `IsEventThrottlingActive`,
  `ThrottleInterval`, `DebounceInterval`: while throttled, many changes of one property between two
  ticks give ONE `PropertyChanged` at the tick; with a debounce, a property is sent only after it
  has been quiet for the debounce interval;
- `EventsOff()`, `EventsOn()`, `EventsOnAndNotifyAll()` (raises once with a null name): while off,
  neither event is raised;
- `SetComplexProperty<TValue>(ref TValue? field, TValue? value, [CallerMemberName] name)`
  (protected; `TValue : class, INotifyPropertyChanged`): sets a property whose value notifies its
  own changes — a change inside the value notifies this property (a `PropertyContainer` value is
  followed through its `MemberChanged`, at once; any other through `PropertyChanged`); the
  previous value is no longer followed (the old code never let it go: a leak);
- the clock: `PropertyContainer(TimeProvider? timeProvider = null)` (default `TimeProvider.System`),
  for the pump and the debounce;
- `IDisposable` (`Dispose()` + `protected virtual Dispose(bool)`): stops following the nested
  values and stops every later event.

The internal `ThrottlePump` (`ComponentModel/ThrottlePump.cs`) is one timer per (`TimeProvider`,
interval) shared by every container with that interval (`TimeProvider.CreateTimer`); a tick posts
to the UI thread the flush of the containers that asked since the previous tick; a container that
asks again during the flush waits for the next tick. The internal `PendingChange` counts the changes
of one property, the ones sent and the time of the last.

Capability: old `../../tmp/zerox/Zerox.Wpf.Core/Core/Containers/PropertyContainer.cs` (WPF
`DispatcherTimer` pump, `BootStrapper` dispatcher) and `MemberContainer.cs`; tests
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/PropertyContainers/ThrotlingTest.cs` (`Test_Throttle`,
`Test_Debounce`: real time, `Thread.Sleep`, ±10 %) and `NestedPropertyChangedTest.cs`
(`Test_Nested`, `Test_Memory_leaks_Nested`, which asserted `weak.IsAlive` — it pinned the leak).
The ported tests are deterministic on `FakeTimeProvider` + `Dispatcher.UIThread.RunJobs()`.
Dropped: identity by Guid (`IHasGuid`), `InvokeOnUiThread*` / `DoUiActionAsync` (Avalonia's
`Dispatcher` is used directly), `InvalidateCommands` (WPF `CommandManager`; block 02's commands
re-query from a source), the `PropertyChangedChainEventArgs` origin chain.

Files: `src/Zerox.Core.Avalonia/ComponentModel/PropertyContainer.cs`, `ThrottlePump.cs`,
`PendingChange.cs`; `tests/Zerox.Core.Avalonia.Tests/Zerox.Core.Avalonia.Tests.csproj` (+
`<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />`); tests
`tests/Zerox.Core.Avalonia.Tests/ComponentModel/PropertyContainerTests.cs`.
</intro>

<red>
- test: Should_RaisePropertyChangedOnTheUiThread_WhenAPropertyChangesOnAnotherThread — `Name` set inside `Task.Run`: `MemberChanged` seen on that thread (not the Avalonia context), `PropertyChanged` "Name" seen on the UI thread
- test: Should_RaiseMemberChangedAtOnce_WhenAPropertyChanges — on the UI thread: right after the set `MemberChanged` once and `PropertyChanged` not yet; after `RunJobs()` `PropertyChanged` once
- test: Should_RaiseNothing_WhenEventsAreOff — `EventsOff()`, set: no event of either kind after `RunJobs()`; `EventsOnAndNotifyAll()` → one `MemberChanged` and one `PropertyChanged` with a null name
- test: Test_Throttle — interval 250 ms on a fake clock: 5000 sets of `Name` → no `PropertyChanged` before the tick; `Advance(250 ms)` + `RunJobs()` → exactly one, and `Name` is the last value; 10 more sets + one tick → one more; a tick without changes → none; `MemberChanged` was raised for every set (5010)
- test: Test_Debounce — interval 100 ms, debounce 1000 ms: a set every 50 ms for 2 s (advancing the clock, running the jobs) → no `PropertyChanged`; then 1100 ms of quiet in 100 ms steps → exactly one
- test: Should_SendWhatIsWaiting_WhenThrottlingStops — throttled, `Name` set, `StopEventThrottling()` + `RunJobs()` → one `PropertyChanged` "Name"; a later set is posted again without a tick
- test: Should_RaiseNothing_WhenDisposed — throttled with a change waiting: `Dispose()`, a tick → no `PropertyChanged`; a set after `Dispose()` raises nothing
- test: Test_Nested — the old scenario: a container with `Name` and a nested container `ChildProp`: setting the child, then `Name` → `MemberChanged` counter 2; `child.Name` set → 3
- test: Test_Memory_leaks_Nested — `ChildProp` replaced 1000 times after the first child: the first child is collectible (`WeakReference.IsAlive` false after `GC.Collect`) — the old test asserted the leak
- test: Should_StopFollowingTheOldValue_WhenReplaced — after `ChildProp` is replaced, a change of the old child raises nothing on the parent; a change of the new one does
- test: Should_FollowAnyNotifyingValue_WhenItIsNotAContainer — a nested value that is a plain `NotifyingObject`: its change raises the parent's `MemberChanged` for the property
- test: Should_RejectTheInterval_WhenItIsNotPositive — `ActivateEventThrottling(TimeSpan.Zero)` → `ArgumentOutOfRangeException`; a negative debounce too
</red>

### Implementation

`PendingChange.cs`:

```csharp
namespace Zerox.Core.Avalonia.ComponentModel;

// One property waiting for the pump: how many changes, how many sent, when the last one arrived.
internal sealed class PendingChange
{
    private long _changes;
    private long _sent;
    private long _lastChange;

    public bool HasUnsent => Interlocked.Read(ref _sent) < Interlocked.Read(ref _changes);

    public void Mark(long timestamp)
    {
        Interlocked.Exchange(ref _lastChange, timestamp);
        Interlocked.Increment(ref _changes);
    }

    // Due when it has unsent changes and has been quiet for the debounce (none: at once).
    public bool IsDue(TimeProvider time, TimeSpan debounce)
        => HasUnsent && (debounce == TimeSpan.Zero || time.GetElapsedTime(Interlocked.Read(ref _lastChange)) >= debounce);

    public void Commit() => Interlocked.Exchange(ref _sent, Interlocked.Read(ref _changes));
}
```

`ThrottlePump.cs`:

```csharp
using System.Collections.Concurrent;
using Avalonia.Threading;

namespace Zerox.Core.Avalonia.ComponentModel;

// One timer per (clock, interval), shared by every container with that interval: a tick flushes, on the
// UI thread, the containers that asked since the previous tick. Pumps live as long as the process.
internal sealed class ThrottlePump
{
    private static readonly Dictionary<(TimeProvider, TimeSpan), ThrottlePump> Pumps = [];
    private readonly ConcurrentQueue<PropertyContainer> _waiting = new();
    private readonly ITimer _timer;   // referenced here so the timer lives

    private ThrottlePump(TimeProvider time, TimeSpan interval)
        => _timer = time.CreateTimer(_ => Dispatcher.UIThread.Post(Tick), null, interval, interval);

    public static ThrottlePump For(TimeProvider time, TimeSpan interval)
    {
        lock (Pumps)
        {
            if (!Pumps.TryGetValue((time, interval), out var pump))
            {
                pump = new ThrottlePump(time, interval);
                Pumps.Add((time, interval), pump);
            }
            return pump;
        }
    }

    public void Enqueue(PropertyContainer container) => _waiting.Enqueue(container);

    // Only the containers waiting when the tick starts: one that asks again during the flush waits for the next tick.
    private void Tick()
    {
        var count = _waiting.Count;
        for (var i = 0; i < count && _waiting.TryDequeue(out var container); i++)
            container.Flush();
    }
}
```

If the analyzers report the `_timer` field (never read, or a disposable field in a type that is not
disposable), suppress that one rule on that field with the justification "the field keeps the
timer alive for the life of the process"; nothing else changes.

`PropertyContainer.cs`:

```csharp
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

    protected override void OnPropertyChanged(string? propertyName)
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
```

Test shape:

```csharp
public sealed class Sample(TimeProvider? time = null) : PropertyContainer(time)
{
    private string _name = "";
    private Sample? _childProp;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public Sample? ChildProp
    {
        get => _childProp;
        set => SetComplexProperty(ref _childProp, value);
    }
}

[Fact]
public Task Test_Throttle() => HeadlessUi.RunAsync(() =>
{
    var clock = new FakeTimeProvider();
    var container = new Sample(clock);
    container.ActivateEventThrottling(TimeSpan.FromMilliseconds(250));
    var raised = 0;
    container.PropertyChanged += (_, e) => raised += e.PropertyName == "Name" ? 1 : 0;

    for (var i = 0; i < 5000; i++)
        container.Name = $"Name{i}";
    Dispatcher.UIThread.RunJobs();
    raised.Should().Be(0);

    clock.Advance(TimeSpan.FromMilliseconds(250));
    Dispatcher.UIThread.RunJobs();
    raised.Should().Be(1);
    container.Name.Should().Be("Name4999");
    return Task.CompletedTask;
});
```

<success>
- [ ] `PropertyContainerTests` pass (12 tests), the ported ones without real waits
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
