# Editing

<block id="03-poco-clone-compare">
## TDDAB-3: Port the Deep Clone and the Comparison of POCOs

<intro>
Delivers `PocoExtensions` (static, `src/Zerox.Core/Editing/PocoExtensions.cs`) on the two
libraries the user keeps (FastDeepCloner 1.3.6, ObjectsComparer 1.4.1, from NuGet):
- `CloneByFields<T>(this T original)` (`T : class`): a deep copy made field by field (private
  fields too, nested objects and lists copied, nothing shared); null → `ArgumentNullException`;
- `IsChanged<T>(this T? original, T? other)`: false for two nulls, true for one null; a type that
  is `IComparable<T>` decides with `CompareTo`; otherwise ObjectsComparer compares member by member;
- `IsChanged<T>(this T? original, T? other, out IReadOnlyList<MemberDifference> differences)`: the
  same with the differences as `MemberDifference(string MemberPath, string? Value1, string? Value2)`
  (our record: the package types do not leak into the API);
- `JsonDefaults.Options` (stage A) also reads and writes PUBLIC FIELDS (`IncludeFields = true`):
  the POCOs of the edit copy have fields; it stays the one JSON setup of the library.

Packages: `Directory.Packages.props` gets `<PackageVersion Include="FastDeepCloner" Version="1.3.6" />`
and `<PackageVersion Include="ObjectsComparer" Version="1.4.1" />`; `src/Zerox.Core/Zerox.Core.csproj`
references both (no version in the `.csproj`).

Capability: old `../../tmp/zerox/Zerox.Wpf.Core/Core/Support/PocoExtensions.cs` (`PocoCloneByFields`,
`IsChanged`, `IsChanged(out List<Difference>)`) on the same two libraries compiled from source.
Fixed: a null original threw `ApplicationException` (now `ArgumentNullException`); the difference
list exposed the package's `Difference`.

Files: `src/Zerox.Core/Editing/PocoExtensions.cs`, `src/Zerox.Core/Text/JsonDefaults.cs`
(one line), `Directory.Packages.props`, `src/Zerox.Core/Zerox.Core.csproj`; tests
`tests/Zerox.Core.Tests/Editing/PocoExtensionsTests.cs`, `tests/Zerox.Core.Tests/Text/JsonDefaultsTests.cs` (one new test).
</intro>

<red>
- test: Should_CloneDeeply_WhenCloningByFields — a POCO with a public field, a private field, a `List<int>` and a nested object: the clone has equal values, its list and nested object are other instances, changing the clone's nested value leaves the original unchanged
- test: Should_RejectNull_WhenCloning — `CloneByFields<Sample>(null!)` throws `ArgumentNullException`
- test: Should_ReportNoChange_WhenTheCopyIsEqual — `original.IsChanged(original.CloneByFields())` is false
- test: Should_ListTheDifferences_WhenAMemberDiffers — nested `X` 3 → 9 in the copy: `IsChanged(copy, out differences)` true, one difference with `MemberPath` "Child.X", `Value1` "3", `Value2` "9"
- test: Should_UseCompareTo_WhenTheTypeIsComparable — a type whose `CompareTo` compares only `Id`: two objects with the same `Id` and different names are not changed
- test: Should_DecideOnNulls_WhenOneOrBothAreNull — two nulls: not changed; one null: changed
- test: Should_WriteAndReadPublicFields_WhenUsingTheJsonDefaults — a class with public fields `Name` and `Count` round-trips through `ToJson` / `FromJson` with its values (`{"Name":…,"Count":…}`)
</red>

### Implementation

```csharp
using FastDeepCloner;

namespace Zerox.Core.Editing;

// A member that differs between two objects (its path and the two values as text).
public sealed record MemberDifference(string MemberPath, string? Value1, string? Value2);

// Deep clone by fields and member-by-member comparison of plain objects.
public static class PocoExtensions
{
    public static T CloneByFields<T>(this T original)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(original);
        return (T)DeepCloner.Clone(original, new FastDeepClonerSettings { FieldType = FieldType.FieldInfo });
    }

    public static bool IsChanged<T>(this T? original, T? other)
        where T : class
    {
        if (original is null || other is null)
            return !(original is null && other is null);
        if (original is IComparable<T> comparable)
            return comparable.CompareTo(other) != 0;
        return !new ObjectsComparer.Comparer<T>().Compare(original, other);
    }

    public static bool IsChanged<T>(this T? original, T? other, out IReadOnlyList<MemberDifference> differences)
        where T : class
    {
        if (original is null || other is null)
        {
            differences = [];
            return !(original is null && other is null);
        }
        var same = new ObjectsComparer.Comparer<T>().Compare(original, other, out var found);
        differences = [.. found.Select(difference => new MemberDifference(difference.MemberPath, difference.Value1, difference.Value2))];
        return !same;
    }
}
```

`JsonDefaults.Create()` gets `IncludeFields = true`:

```csharp
var options = new JsonSerializerOptions { PropertyNamingPolicy = null, IncludeFields = true };
```

<success>
- [ ] `PocoExtensionsTests` pass (6 tests) and the new `JsonDefaultsTests` test passes with the old ones
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a), NuGet audit included
</success>
</block>

<block id="04-poco-proxy">
## TDDAB-4: Port the Edit Copy

<intro>
Delivers `PocoProxy<T>` (abstract, `T : class, new()`, derives from block 01's `NotifyingObject`),
the edit copy a view model of a dialog or a node's parameters wraps around a POCO:
- `protected T Data`: the data being edited; a derived class exposes it with properties —
  `set => SetProperty(ref Data.Name, value)` for a POCO with fields,
  `set => SetProperty(x => x.Name, value)` for a POCO with properties (the second overload uses
  stage A's `FastProperty`; an expression that does not select a property → `ArgumentException`);
- constructors: empty (new `T` for data and saved state) and copy (`PocoProxy<T> source`: data and
  saved state are clones of the source's data);
- `IsChanged` (data vs saved state, block 03's `IsChanged`), notified ONLY after a real change
  (`SetProperty` returned true); `SaveState()` (saved state = clone of data, notifies `IsChanged`),
  `ResetState()` (data = clone of the saved state, notifies every property: null name),
  `GetState()` (a clone of the data), `SetState(T)` (data = a clone of it, notifies every property),
  `DataEquals(PocoProxy<T> other)` (same data), `SaveStateOf(string member)` (protected: saves one
  field or property only; unknown member → `ArgumentException`);
- JSON with `JsonDefaults.Options`: `ToJson()`, `Populate(string json)`,
  `SerializeAsync(Stream, CancellationToken)`, `PopulateAsync(Stream, CancellationToken)`; a populate
  deserializes first and replaces the data only on success, so a bad document leaves the data as it
  was without a `catch` (the exception reaches the caller); a document holding `null` →
  `JsonException`.

Capability: old `../../tmp/zerox/Zerox.Wpf.Core/Core/Containers/PocoProxy.cs` (on `PropertyContainer`
and Newtonsoft; `ISerializableIntoJson` with `JsonWriter`/`JsonReader`) and its tests
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/PropertyContainers/PocoProxyTest.cs` (`Test_Changed`,
`Test_Copy_Stream`, `Test_Copy_String`; the `SomeData` POCO with fields + `SomeDataProxy`, the
`SomeDataProps` POCO with properties + `SomeDataPropsProxy`). Changed: `_data` → `Data`;
`IsChanged` was notified on every set even without a change; `InvalidateCommands` is stage D;
`GetState` returned the live data (now a clone).

Files: `src/Zerox.Core/Editing/PocoProxy.cs`; tests `tests/Zerox.Core.Tests/Editing/PocoProxyTests.cs`.
</intro>

<red>
- test: Test_Changed — the old scenario on both proxies (fields and properties): after `SaveState` not changed; a name change → changed; back → not changed; change + `SaveState` → not changed; `ResetState` → not changed; a copy `b` of `a`: changing `a` leaves `b` unchanged with the saved name; `DataEquals` false, then true after `b.Name = a.Name`
- test: Test_Copy_Stream — `a.SerializeAsync(stream)`, rewind, `b.PopulateAsync(stream)`: `a.DataEquals(b)`
- test: Test_Copy_String — `b.Populate(a.ToJson())`: `a.DataEquals(b)`
- test: Should_NotifyIsChanged_WhenAPropertyReallyChanges — setting a new name raises "Name" and "IsChanged"; setting the same name raises nothing
- test: Should_NotifyEveryProperty_WhenTheStateIsReset — `ResetState()` and `SetState(...)` raise one event with a null name
- test: Should_KeepTheData_WhenPopulateFails — `Populate("{ not json")` throws `JsonException` and the data and `IsChanged` are as before; `Populate("null")` throws `JsonException`
- test: Should_SaveOneMember_WhenSaveStateOfIsCalled — name and hue changed, `SaveStateOf("Name")`: still changed; reverting the hue → not changed
- test: Should_ReturnACopy_WhenGetStateIsCalled — changing the object `GetState()` returned leaves the proxy's data unchanged
- test: Should_RejectTheExpression_WhenItSelectsNoProperty — `SetProperty(x => x.ToString(), …)` throws `ArgumentException`
</red>

### Implementation

```csharp
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Zerox.Core.ComponentModel;
using Zerox.Core.Reflection;
using Zerox.Core.Text;

namespace Zerox.Core.Editing;

// An edit copy of a plain object: the data being edited and the saved state; IsChanged compares them.
public abstract class PocoProxy<T> : NotifyingObject
    where T : class, new()
{
    private T _saved;

    protected PocoProxy()
    {
        Data = new T();
        _saved = new T();
    }

    protected PocoProxy(PocoProxy<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Data = source.Data.CloneByFields();
        _saved = source.Data.CloneByFields();
    }

    public bool IsChanged => Data.IsChanged(_saved);

    // The data being edited (fields by ref: SetProperty(ref Data.Name, value)).
    protected T Data { get; private set; }

    public T GetState() => Data.CloneByFields();

    public void SetState(T state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Replace(state.CloneByFields());
    }

    public void SaveState()
    {
        _saved = Data.CloneByFields();
        NotifyPropertyChanged(nameof(IsChanged));
    }

    public void ResetState() => Replace(_saved.CloneByFields());

    public bool DataEquals(PocoProxy<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return !Data.IsChanged(other.Data);
    }

    public string ToJson() => JsonSerializer.Serialize(Data, JsonDefaults.Options);

    // Deserializes first: a bad document leaves the data as it was.
    public void Populate(string json)
        => Replace(JsonSerializer.Deserialize<T>(json, JsonDefaults.Options) ?? throw new JsonException("The document holds no data"));

    public Task SerializeAsync(Stream stream, CancellationToken cancellationToken = default)
        => JsonSerializer.SerializeAsync(stream, Data, JsonDefaults.Options, cancellationToken);

    public async Task PopulateAsync(Stream stream, CancellationToken cancellationToken = default)
        => Replace(await JsonSerializer.DeserializeAsync<T>(stream, JsonDefaults.Options, cancellationToken).ConfigureAwait(false)
                   ?? throw new JsonException("The document holds no data"));

    protected override bool SetProperty<TValue>(ref TValue field, TValue value, [CallerMemberName] string? propertyName = null)
    {
        if (!base.SetProperty(ref field, value, propertyName))
            return false;
        NotifyPropertyChanged(nameof(IsChanged));
        return true;
    }

    // For a data object with properties: SetProperty(x => x.Name, value).
    protected bool SetProperty<TValue>(Expression<Func<T, TValue>> member, TValue value, [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(member);
        var property = (member.Body as MemberExpression)?.Member as PropertyInfo
            ?? throw new ArgumentException("The expression must select a property of the data", nameof(member));
        var accessor = FastProperty.For<T, TValue>(property);
        if (EqualityComparer<TValue>.Default.Equals(accessor.Get(Data), value))
            return false;
        accessor.Set(Data, value);
        NotifyPropertyChanged(propertyName);
        NotifyPropertyChanged(nameof(IsChanged));
        return true;
    }

    // Saves one field or property only; the other members keep their saved state.
    protected void SaveStateOf(string member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var type = typeof(T);
        if (type.GetField(member) is { } field)
            field.SetValue(_saved, field.GetValue(Data));
        else if (type.GetProperty(member) is { } property)
            property.SetValue(_saved, property.GetValue(Data));
        else
            throw new ArgumentException($"{type.Name} has no member {member}", nameof(member));
        NotifyPropertyChanged(nameof(IsChanged));
    }

    private void Replace(T data)
    {
        Data = data;
        NotifyPropertyChanged(propertyName: null);
    }
}
```

<success>
- [ ] `PocoProxyTests` pass (9 tests)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
