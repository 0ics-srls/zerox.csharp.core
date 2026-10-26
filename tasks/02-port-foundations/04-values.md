# Values: identity, numbers, text, conversion, reflection

<block id="09-hier-id">
## TDDAB-9: Port the Hierarchical Id as an Immutable Value

<intro>
Delivers `HierId`: an id made of parts and a separator (`P1/P2/P3`), with parsing, prefixes,
suffix matching, pattern matching, prefix swap and value equality. Used later by the statistics
aggregator (series indexed by `HierId`, groups by prefix) and by assetforge for hierarchical names.

Capability: the old `../../tmp/zerox/Zerox.Wpf.Core/Core/Support/HierId.cs` (tests
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Support/HierIdTest.cs`, NUnit, 11 tests). Rewritten
immutable (the old `+ string`, `- string` and `Append` changed their left operand; the old test
`Test_Operator` passes because it reassigns). Bugs fixed: `GetParts(n)` dropped the separator;
`Splice(n)` with `n ≥ count` returned the last part and `Splice(0)` returned an empty id — now
`Splice(0)` is the same id and `Splice(n ≥ count)` is empty; `TryGetPart` dropped (it caught an
exception that `GetPart` never throws). Kept as they were: `GetPart(i)` with `i` beyond the last
part returns the last part, an empty id returns `""`; `==` with a `string` compares the text;
`ToString(levels)` with `levels ≤ 0` prints all parts.

Equality: same separator and same parts (ordinal). `Size()` becomes the property `Count`.

Files: `src/Zerox.Core/Identity/HierId.cs`; tests `tests/Zerox.Core.Tests/Identity/HierIdTests.cs`
(the 11 old tests with their names, NUnit → xunit, plus the new ones).
</intro>

<red>
- test: Test_Parse — `Parse("P1/P2")` and `Parse("P1\\P2", '\\')` give the separator and the two parts
- test: Test_From — `From(parts)`, `From(parts, sep)`, `From(id, sep)`, `From(id)`, `From("P1", "P2")` as in the old test
- test: Test_ToString — `"P1/P2"`; `ToString(-1)`, `ToString(0)`, `ToString(3)`, `ToString(4)` give all parts, `ToString(1)` and `ToString(2)` the first one and two
- test: Test_Operator — the old test (`+=` string, `+` string, `-=` string, `+` id; the addends of `+` are unchanged)
- test: Test_Operators_eq_neq — the old test (`==`/`!=` between ids and with strings)
- test: Test_GetNumberOfEndingMatchingParts — the old test
- test: GetParts — the old test
- test: Test_Splice — the old test
- test: Test_Swap_Prefix — the old test (`CopyWithNewPrefix`)
- test: Test_StartsWith — the old test
- test: Test_Operators_partial_pattern — the old test (`GetSimilarRootSize`, `MatchPartsByPatternPms`)
- test: Should_LeaveTheIdUnchanged_WhenAPartIsAddedOrRemoved — `id + "x"`, `id - "P1"`, `id.Append("x")` return new ids and `id` keeps its parts
- test: Should_KeepTheSeparator_WhenTakingTheFirstParts — `Parse("a$b$c", '$').GetParts(2).ToString()` is `"a$b"`
- test: Should_SpliceToTheSameOrToEmpty_WhenNIsZeroOrBeyond — `Splice(0)` equals the id; `Splice(3)` and `Splice(9)` of a 3-part id are empty
- test: Should_HashEqually_WhenIdsAreEqual — equal ids have the same `GetHashCode` and work as dictionary keys
</red>

### Implementation

```csharp
namespace Zerox.Core.Identity;

// An id made of parts: P1/P2/P3. Immutable: every operation returns a new id.
public sealed class HierId : IEquatable<HierId>
{
    public const char DefaultSeparator = '/';

    private readonly ImmutableArray<string> _parts;

    private HierId(IEnumerable<string> parts, char separator)
    {
        _parts = [.. parts];
        Separator = separator;
    }

    public char Separator { get; }
    public IReadOnlyList<string> Parts => _parts;
    public int Count => _parts.Length;

    public static HierId Empty(char separator = DefaultSeparator) => new([], separator);
    public static HierId From(params string[] parts) => new(parts, DefaultSeparator);
    public static HierId From(IEnumerable<string> parts, char separator = DefaultSeparator) => new(parts, separator);
    public static HierId From(HierId from) => new(from._parts, from.Separator);
    public static HierId From(HierId from, char separator) => new(from._parts, separator);
    public static HierId Parse(string text, char separator = DefaultSeparator)
        => text.Length == 0 ? Empty(separator) : new(text.Split(separator), separator);

    public string GetPart(int index) => Count == 0 ? string.Empty : _parts[Math.Min(index, Count - 1)];
    public HierId GetParts(int count) => count > 0 && count <= Count ? new(_parts.Take(count), Separator) : this;
    public HierId Splice(int count = 1)
        => count <= 0 ? this : count >= Count ? Empty(Separator) : new(_parts.Skip(count), Separator);
    public HierId CopyWithNewPrefix(HierId prefix) => prefix + Splice(prefix.Count);
    public HierId Append(string part) => new([.. _parts, part], Separator);
    public HierId Remove(string part) { ... }   // without the first part equal to it

    public int GetSimilarRootSize(HierId other) { ... }               // as the old one
    public int GetNumberOfEndingMatchingParts(HierId other) { ... }   // as the old one
    public bool MatchPartsByPattern(HierId other, IEnumerable<bool> pattern) { ... }   // as the old one
    public bool MatchPartsByPatternPms(HierId other, params bool[] pattern) => MatchPartsByPattern(other, pattern);
    public bool StartsWith(HierId prefix) => GetSimilarRootSize(prefix) == prefix.Count;

    public static HierId operator +(HierId left, string right) => left.Append(right);
    public static HierId operator +(HierId left, HierId right) => new([.. left._parts, .. right._parts], left.Separator);
    public static HierId operator -(HierId left, string right) => left.Remove(right);
    public static bool operator ==(HierId? left, HierId? right) => left is null ? right is null : left.Equals(right);
    public static bool operator !=(HierId? left, HierId? right) => !(left == right);
    public static bool operator ==(HierId? left, string? right) => left?.ToString() == right;
    public static bool operator !=(HierId? left, string? right) => !(left == right);

    public bool Equals(HierId? other)
        => other is not null && Separator == other.Separator && _parts.AsSpan().SequenceEqual(other._parts.AsSpan());
    public override bool Equals(object? obj) => obj is HierId other && Equals(other);
    public override int GetHashCode() { var hash = new HashCode(); hash.Add(Separator); foreach (var part in _parts) hash.Add(part, StringComparer.Ordinal); return hash.ToHashCode(); }

    public override string ToString() => string.Join(Separator, _parts);
    public string ToString(int levels) => string.Join(Separator, _parts.Take(levels > 0 ? Math.Min(levels, Count) : Count));
}
```

If the analyzers ask for named alternatives of the operators (CA2225), `Append`, `Remove` and a
`Concat(HierId)` are those alternatives.

<success>
- [ ] `HierIdTests` pass (15 tests)
- [ ] `HierId` has no mutating member (every field `readonly`, no setter)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="10-number-range">
## TDDAB-10: Rewrite the Typed Number Interval with Generic Math

<intro>
Delivers `NumberRange<T>`: an interval of a numeric type (bounds default to the type's own
limits) that clamps a value, tells whether a value is inside (with or without the bounds), steps
a value up or down saturating at the bounds, multiplies saturating, and parses text into the
interval. This is what an editor of numeric parameters needs (min and max from a schema).

Capability: the old `../../tmp/zerox/Zerox.Wpf.Core/Core/Support/NumberValue.cs` works on `object`
and `Type`, does every computation in `double`, `long` or `ulong`, and its own test says `Int64` and
`UInt64` "can't function on limits" (tests
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Support/NumberParserTest.cs`). Rewritten on .NET generic
math (`INumber<T>`, `IMinMaxValue<T>`): one implementation for every numeric type, `long` and
`ulong` included. Behaviour kept: text over the maximum parses to the maximum, under the minimum
to the minimum (even beyond the type: `"300"` as `byte` is 255); invalid text gives no value;
a step that would leave the interval stops at the bound. `TryParse` uses the given format provider
(the old one used a mutable static). `AddInTypedIntervalUsingDecimal` and `DeltaInTypedInterval`
are dropped (with `T = decimal` the first is `Add`; the second is a subtraction).

Integer types parse with `NumberStyles.Integer` (so `"1.5"` is invalid for `int`), the others with
`NumberStyles.Float`; text beyond the type's range is read as a `BigInteger` (integers) or a
`double` (others) and saturated into `T`.

`ToString()` prints `( min, max )`; a bound equal to the type's limit whose text is longer than 5
characters prints as `-∞` / `∞` (as the old `ClumpedTypedIntervalString`).

Files: `src/Zerox.Core/Numbers/NumberRange.cs`; tests `tests/Zerox.Core.Tests/Numbers/NumberRangeTests.cs`
(new names; cases taken from the old test for `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`,
plus `long`, `ulong`, `float`, `double`, `decimal`).
</intro>

<red>
- test: Should_ClampToTheTypeLimits_WhenTheTextIsBeyondThem — for every integer type: the type's max/min text parses to max/min, max+1 / min−1 (as text) parse to max / min
- test: Should_ClampToTheInterval_WhenTheTextIsOutsideIt — `NumberRange<int>(-10, 10)`: `"50"` → 10, `"-50"` → -10, `"5"` → 5
- test: Should_GiveNoValue_WhenTheTextIsNotANumber — `"abc"` and `""` → `TryParse` false; `"1.5"` false for `int`, true (1.5) for `double`
- test: Should_SaturateAtTheBounds_WhenStepping — `NumberRange<int>(0, 100)`: `Add(95, 10)` = 100, `Add(5, -10)` = 0, `Add(50, 10)` = 60; the same at the type limits for `long` and `ulong` without overflow
- test: Should_SaturateAtTheBounds_WhenMultiplying — `NumberRange<byte>.Full.Multiply(100, 3)` = 255
- test: Should_TellWhetherAValueIsInside_WhenCheckingWithAndWithoutBounds — `Contains(10)` true and `ContainsExclusive(10)` false for `(0, 10)`; `Contains(11)` false
- test: Should_KeepDecimals_WhenTheTypeHasThem — `NumberRange<decimal>(0m, 1m).Add(0.1m, 0.2m)` = 0.3m exactly; `NumberRange<double>` parses `"0.25"` with the invariant culture
- test: Should_DescribeTheType_WhenAskedForIntegerAndSign — `NumberRange<byte>.IsInteger` true, `IsSigned` false; `NumberRange<double>.IsInteger` false, `IsSigned` true
- test: Should_PrintInfinity_WhenABoundIsALongTypeLimit — `NumberRange<int>.Full.ToString()` = `( -∞, ∞ )`, `NumberRange<byte>.Full.ToString()` = `( 0, 255 )`
- test: Should_Throw_WhenMinIsGreaterThanMax — `new NumberRange<int>(5, 1)` throws `ArgumentException`
</red>

### Implementation

```csharp
namespace Zerox.Core.Numbers;

// An interval of a numeric type: clamp, contain, step and parse inside it.
public readonly record struct NumberRange<T>
    where T : struct, INumber<T>, IMinMaxValue<T>
{
    private static readonly bool Integer = typeof(T).GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBinaryInteger<>));

    public NumberRange(T min, T max)
    {
        if (min > max)
            throw new ArgumentException($"Min {min} is greater than max {max}", nameof(min));
        Min = min;
        Max = max;
    }

    public T Min { get; }
    public T Max { get; }

    public static NumberRange<T> Full => new(T.MinValue, T.MaxValue);
    public static NumberRange<T> Of(T? min, T? max) => new(min ?? T.MinValue, max ?? T.MaxValue);
    public static bool IsInteger => Integer;
    public static bool IsSigned => T.IsNegative(T.MinValue);

    public T Clamp(T value) => T.Clamp(value, Min, Max);
    public bool Contains(T value) => value >= Min && value <= Max;
    public bool ContainsExclusive(T value) => value > Min && value < Max;

    // value + step, stopping at the bounds
    public T Add(T value, T step) => Saturate(() => checked(value + step), T.IsNegative(step));

    // value * factor, stopping at the bounds
    public T Multiply(T value, T factor) => Saturate(() => checked(value * factor), T.IsNegative(value) != T.IsNegative(factor));

    // An overflow is the expected outcome of saturation: it is handled right here and becomes the bound.
    private T Saturate(Func<T> operation, bool towardsMin)
    {
        try
        {
            return Clamp(operation());
        }
        catch (OverflowException)
        {
            return towardsMin ? Min : Max;
        }
    }

    public bool TryParse(string? text, IFormatProvider? provider, out T value) { ... }
    public override string ToString() { ... }
}
```

`checked` on generic operands calls the checked operators of generic math: integer types and
`decimal` throw `OverflowException` at the type limit (caught in `Saturate`, the one `catch` of the
file), `float` and `double` go to infinity and `Clamp` brings them back to the bound.
`TryParse`: `T.TryParse(text, style, provider, out parsed)` → `Clamp(parsed)`; when it fails and
the text is a number beyond the type (`BigInteger.TryParse` with `NumberStyles.Integer` for integer
types, `double.TryParse` with `NumberStyles.Float` for the others), `Clamp(T.CreateSaturating(...))`;
otherwise false with `value = default`.

<success>
- [ ] `NumberRangeTests` pass (10 tests, every listed type)
- [ ] No `object` / `Type` parameter in `src/Zerox.Core/Numbers`
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="11-text-and-json">
## TDDAB-11: Add the Text Helpers and the One JSON Setup

<intro>
Delivers two things that every later stage uses:
- `TextExtensions`: `Truncate(maxLength)` (cut with `"..."` and never longer than `maxLength`),
  `Clip(maxLength)` (cut without ellipsis; `maxLength ≤ 0` means no limit; `null` stays `null`),
  `ToBase64Utf8()`, `FromBase64Utf8(fallback)` (empty text → fallback; invalid Base64 throws
  `FormatException` to the caller).
- `JsonDefaults`: the JSON options of the whole library on System.Text.Json — property names
  as they are in C# (no naming policy), enums written as their member names, nulls written — and
  `ToJson` / `FromJson` for strings and `ToJsonAsync` / `FromJsonAsync` for streams.

Capability: old `../../tmp/zerox/Zerox.Wpf.Core/Core/Support/StringExtensions.cs` (`TruncString`,
`CheckMaxLength`, Base64; no tests) and the JSON helpers spread over `StringExtensions.JsonToObject`,
`ObjectExtensions.ToJson`, `StreamJsonExtensions` (Newtonsoft, three option sets). Written ONCE
on System.Text.Json (DRY). Bug fixed: `TruncString` with a limit ≤ 3 returned `"..."`, longer
than the limit; now with a limit ≤ 3 the text is cut without ellipsis. Dropped: the WPF
`FlowDocument` builder and `SecureString` conversion, MD5 helpers.

Files: `src/Zerox.Core/Text/TextExtensions.cs`, `src/Zerox.Core/Text/JsonDefaults.cs`; tests
`tests/Zerox.Core.Tests/Text/TextExtensionsTests.cs`, `tests/Zerox.Core.Tests/Text/JsonDefaultsTests.cs`.
</intro>

<red>
- test: Should_KeepTheText_WhenItFitsTheLimit — `"abc".Truncate(5)` = `"abc"`, `"abc".Clip(3)` = `"abc"`
- test: Should_EndWithEllipsisWithinTheLimit_WhenTruncating — `"abcdefghij".Truncate(6)` = `"abc..."` (length 6)
- test: Should_CutWithoutEllipsis_WhenTheLimitIsThreeOrLess — `"abcdef".Truncate(2)` = `"ab"`, `Truncate(0)` = `""`
- test: Should_NotLimit_WhenClipMaxLengthIsZeroOrNegative — `"abcdef".Clip(0)` = `"abcdef"`; `((string?)null).Clip(3)` is null
- test: Should_RoundTripUtf8_WhenEncodingToBase64 — `"città €".ToBase64Utf8().FromBase64Utf8()` = `"città €"`; `"".FromBase64Utf8("x")` = `"x"`
- test: Should_ThrowFormat_WhenTheBase64IsInvalid — `"%%%".FromBase64Utf8()` throws `FormatException`
- test: Should_WriteNamesAsTheyAre_WhenSerializing — a record `Sample(string Name, SampleKind Kind, int? Count)` serializes to `{"Name":"a","Kind":"Second","Count":null}`
- test: Should_ReadEnumsByMemberName_WhenDeserializing — `{"Name":"a","Kind":"Second","Count":3}` deserializes to `Sample("a", SampleKind.Second, 3)`
- test: Should_RoundTripThroughAStream_WhenUsingTheAsyncMethods — `ToJsonAsync` to a `MemoryStream` then `FromJsonAsync` gives an equal record
</red>

### Implementation

```csharp
namespace Zerox.Core.Text;

public static class TextExtensions
{
    private const string Ellipsis = "...";

    public static string Truncate(this string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        if (text.Length <= maxLength)
            return text;
        return maxLength <= Ellipsis.Length ? text[..maxLength] : string.Concat(text.AsSpan(0, maxLength - Ellipsis.Length), Ellipsis);
    }

    public static string? Clip(this string? text, int maxLength)
        => text is not null && maxLength > 0 && text.Length > maxLength ? text[..maxLength] : text;

    public static string ToBase64Utf8(this string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    public static string FromBase64Utf8(this string base64, string fallback = "")
        => string.IsNullOrEmpty(base64) ? fallback : Encoding.UTF8.GetString(Convert.FromBase64String(base64));
}

// The JSON setup of the whole library: names as in C#, enums as member names.
public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static string ToJson<T>(this T value) => JsonSerializer.Serialize(value, Options);
    public static T? FromJson<T>(this string json) => JsonSerializer.Deserialize<T>(json, Options);
    public static Task ToJsonAsync<T>(this Stream stream, T value, CancellationToken cancellationToken = default)
        => JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken);
    public static ValueTask<T?> FromJsonAsync<T>(this Stream stream, CancellationToken cancellationToken = default)
        => JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken);

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
```

`System.Text.Json` is part of the shared framework: no package is added.

<success>
- [ ] `TextExtensionsTests` and `JsonDefaultsTests` pass (9 tests)
- [ ] No `Newtonsoft` anywhere in the repository (`grep -rn Newtonsoft src tests` is empty)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="12-value-conversion">
## TDDAB-12: Write the One Value Conversion

<intro>
Delivers `ValueConversion.ConvertTo(object? value, Type target)` and `ConvertTo<T>(this object? value)`:
`null` and `DBNull` → `null` for a reference or nullable target, the type's default otherwise;
a value already of the target type → itself; `Nullable<U>` → converted to `U`; enum target → from
its member name (string, case-sensitive) or from an integral number; otherwise
`Convert.ChangeType` with the invariant culture. A value that cannot be converted throws the
framework's exception (`InvalidCastException`, `FormatException`, `OverflowException`,
`ArgumentException` for an unknown enum name) to the caller.

Capability: the old code had it twice — `TypeExtensions.To<T>` (enum by number, `null` → default,
every error wrapped into `ApplicationException`) and `ConversionExtensions.Convert` (nullable
unwrap) in `../../tmp/zerox/Zerox.Wpf.Core/Core/Support/`. ONE implementation here (DRY); the
wrapping is dropped (the original exception reaches the caller). `SafeCast` (= `OfType<T>()`),
`GetKnownTypeByName`, `IsDescendantOfTypeByName` are dropped (no consumer). Tests: the cases of
the old `TypeExtensionsTest` (`Test_Cast_to_int`, `Test_Cast_to_Enum`, `Test_Cast_with_null`)
plus the new ones.

Files: `src/Zerox.Core/Text/ValueConversion.cs`; tests
`tests/Zerox.Core.Tests/Text/ValueConversionTests.cs`.
</intro>

<red>
- test: Test_Cast_to_int — `(byte)0x7f`, `(short)0x7fff`, `(ushort)0xffff`, `(long)0x7fffffff`, `(ulong)0xffffffff`, `(int)0x7fffffff`, `SomeOtherEnum.C` convert to the `long` values of the old test
- test: Test_Cast_to_Enum — `16` converts to `SomeOtherEnum.C`
- test: Test_Cast_with_null — `null` converts to `0` for `int`
- test: Should_ReturnNull_WhenTheTargetIsNullableAndTheValueIsNull — `ConvertTo(null, typeof(int?))` and `ConvertTo(DBNull.Value, typeof(string))` are null
- test: Should_ConvertTheUnderlyingType_WhenTheTargetIsNullable — `"42"` to `int?` is 42
- test: Should_ReadTheEnumByName_WhenTheValueIsText — `"B"` to `SomeOtherEnum` is `SomeOtherEnum.B`; `"Nope"` throws `ArgumentException`
- test: Should_UseTheInvariantCulture_WhenConvertingText — `"1.5"` to `double` is 1.5 under any current culture (the test sets `it-IT`)
- test: Should_LetTheFrameworkExceptionReachTheCaller_WhenTheValueCannotBeConverted — `"abc"` to `int` throws `FormatException`; `300` to `byte` throws `OverflowException`
- test: Should_ReturnTheSameInstance_WhenTheValueIsAlreadyOfTheTarget — a `List<int>` to `IEnumerable<int>` is the same instance
</red>

### Implementation

```csharp
namespace Zerox.Core.Text;

// Converts a value to a target type; a value that cannot be converted throws the framework's exception.
public static class ValueConversion
{
    public static object? ConvertTo(object? value, Type target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var underlying = Nullable.GetUnderlyingType(target);
        if (value is null || value is DBNull)
            return underlying is not null || !target.IsValueType ? null : Activator.CreateInstance(target);
        var type = underlying ?? target;
        if (type.IsInstanceOfType(value))
            return value;
        if (type.IsEnum)
            return value is string name ? Enum.Parse(type, name) : Enum.ToObject(type, value);
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    public static T? ConvertTo<T>(this object? value) => (T?)ConvertTo(value, typeof(T));
}
```

<success>
- [ ] `ValueConversionTests` pass (9 tests)
- [ ] `ValueConversion` is the only conversion helper in `src` (no other `Convert.ChangeType` call)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="13-reflection">
## TDDAB-13: Port the Reflection Helpers and the Compiled Property Accessor

<intro>
Delivers the two pieces the declarative discovery of later stages stands on:
- `AttributeExtensions`: `GetPropertiesWith<TAttribute>(this Type type)` → every public instance
  or static property carrying `TAttribute` (inherited attributes included) with the attribute;
  `GetPropertyAttribute<TAttribute>(this Type type, string propertyName)`;
  `GetAttributeOfPropertyType<TAttribute>(this Type type, string propertyName)`.
- `FastProperty<TInstance, TValue>`: getter and setter of a property compiled once with
  `System.Linq.Expressions`, then called as delegates (no reflection per call — the aggregator
  samples properties at high rate). Instances are cached per `(PropertyInfo, publicOnly)`.

Capability: old `../../tmp/zerox/Zerox.Wpf.Core/Core/Support/ReflectionExtensions.cs` and
`../../tmp/zerox/Zerox.Wpf.Core/Core/Reflection/*` (no tests). Kept only what the framework lacks:
single-attribute lookups are `CustomAttributeExtensions.GetCustomAttribute<T>` / `IsDefined`
(framework), so `GetAttribute`, `HasAttribute`, `GetAttributeValue` are dropped, and so are the
`IPropertyContainer` overloads. Bug fixed: the accessor cache was a `Dictionary` written by
several threads; now a `ConcurrentDictionary`. A missing getter/setter throws
`InvalidOperationException` (was `ApplicationException`). `strict` becomes `publicOnly` (true:
only public accessors; false: non-public accessors too).

Files: `src/Zerox.Core/Reflection/AttributeExtensions.cs`, `src/Zerox.Core/Reflection/FastProperty.cs`;
tests `tests/Zerox.Core.Tests/Reflection/AttributeExtensionsTests.cs`,
`tests/Zerox.Core.Tests/Reflection/FastPropertyTests.cs`.
</intro>

<red>
- test: Should_ListThePropertiesWithTheAttribute_WhenTheTypeHasThem — a type with two marked and one unmarked property yields the two with their attribute values, in declaration order
- test: Should_IncludeInheritedAttributes_WhenTheBaseTypeMarksAProperty — an overriding property inherits the base marking
- test: Should_FindTheAttributeOfANamedProperty_WhenItExists — `GetPropertyAttribute<T>("Name")` returns it; an unknown name returns null
- test: Should_FindTheAttributeOfThePropertyType_WhenTheTypeIsMarked — `GetAttributeOfPropertyType<T>("Child")` returns the attribute of the property's type
- test: Should_ReadAndWrite_WhenThePropertyIsPublic — `FastProperty<Sample, int>.For(info)` gets and sets `Sample.Count`
- test: Should_ConvertTheValue_WhenTheTypesAreObject — `FastProperty<object, object>.For(info)` gets a boxed `int` and sets it back from a boxed `int`
- test: Should_WriteAPrivateSetter_WhenPublicOnlyIsFalse — a property with a private setter is written with `publicOnly: false`; with `publicOnly: true` `Set` throws `InvalidOperationException`
- test: Should_ReturnTheSameAccessor_WhenAskedTwice — `For(info)` twice returns the same instance, also from 8 threads at once
- test: Should_Throw_WhenThePropertyHasNoGetter — `Get` of a set-only property throws `InvalidOperationException`
</red>

### Implementation

```csharp
namespace Zerox.Core.Reflection;

public static class AttributeExtensions
{
    private const BindingFlags Properties = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;

    public static IEnumerable<(PropertyInfo Property, TAttribute Attribute)> GetPropertiesWith<TAttribute>(this Type type)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(type);
        foreach (var property in type.GetProperties(Properties))
            if (property.GetCustomAttribute<TAttribute>(inherit: true) is { } attribute)
                yield return (property, attribute);
    }

    public static TAttribute? GetPropertyAttribute<TAttribute>(this Type type, string propertyName) where TAttribute : Attribute
        => type.GetProperty(propertyName, Properties)?.GetCustomAttribute<TAttribute>(inherit: true);

    public static TAttribute? GetAttributeOfPropertyType<TAttribute>(this Type type, string propertyName) where TAttribute : Attribute
        => type.GetProperty(propertyName, Properties)?.PropertyType.GetCustomAttribute<TAttribute>(inherit: true);
}

// Getter and setter of a property compiled once; calls are delegates, not reflection.
public sealed class FastProperty<TInstance, TValue>
{
    private static readonly ConcurrentDictionary<(PropertyInfo, bool), FastProperty<TInstance, TValue>> Cache = new();
    private readonly Func<TInstance, TValue>? _get;
    private readonly Action<TInstance, TValue>? _set;

    private FastProperty(PropertyInfo property, bool publicOnly)
    {
        Property = property;
        (_get, _set) = Compile(property, publicOnly);   // as the old MakeDelegates: Expression.Convert / TypeAs for instance and value
    }

    public PropertyInfo Property { get; }
    public bool CanRead => _get is not null;
    public bool CanWrite => _set is not null;

    public static FastProperty<TInstance, TValue> For(PropertyInfo property, bool publicOnly = true)
    {
        ArgumentNullException.ThrowIfNull(property);
        return Cache.GetOrAdd((property, publicOnly), static key => new FastProperty<TInstance, TValue>(key.Item1, key.Item2));
    }

    public TValue Get(TInstance instance) => (_get ?? throw new InvalidOperationException($"{Property.DeclaringType?.Name}.{Property.Name} has no readable getter")).Invoke(instance);
    public void Set(TInstance instance, TValue value) => (_set ?? throw new InvalidOperationException($"{Property.DeclaringType?.Name}.{Property.Name} has no writable setter")).Invoke(instance, value);

    private static (Func<TInstance, TValue>?, Action<TInstance, TValue>?) Compile(PropertyInfo property, bool publicOnly) { ... }
}
```

`GetOrAdd` may build an accessor twice under contention and keep one: the test checks that
callers always receive the same cached instance, which `GetOrAdd` guarantees.

<success>
- [ ] `AttributeExtensionsTests` and `FastPropertyTests` pass (9 tests)
- [ ] No `Dictionary` without synchronization shared between threads in `src/Zerox.Core/Reflection`
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
