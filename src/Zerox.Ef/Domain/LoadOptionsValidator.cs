using System.Collections;
using System.Globalization;
using System.Reflection;
using DevExtreme.AspNet.Data;

namespace Zerox.Ef.Domain;

/// <summary>
/// The validator of the DevExtreme load options: the error is decided here, and it says how to correct.
/// Whoever builds the options by hand gets a field, an operator, a type wrong. Measured on DevExtreme
/// 5.1.0: for a value that does not convert it does not throw, it returns 0 rows silently; a 4-element
/// condition and an "xor" between two conditions return every row; a Sort without Selector is ignored.
/// An empty or full answer taken as good is the worst damage: here it stops first, with the reason and
/// the right form. The rules hold on what DevExtreme really converts.
/// </summary>
public static class LoadOptionsValidator
{
    private static readonly string[] TextOperators = ["contains", "notcontains", "startswith", "endswith"];
    private static readonly string[] IntervalsWithTime = ["hour", "minute", "second"];

    private const string ConditionForm =
        "A condition is written [\"Field\", \"operator\", value], for example [\"Quantity\", \">\", 2]; " +
        "conditions are joined with \"and\" or \"or\": [[…], \"and\", […]].";

    /// <summary>
    /// The operators a condition may use.
    /// </summary>
    public static IReadOnlyList<string> Operators { get; } =
        ["=", "<>", "<", ">", "<=", ">=", "contains", "notcontains", "startswith", "endswith"];

    /// <summary>
    /// The summary types of TotalSummary and GroupSummary.
    /// </summary>
    public static IReadOnlyList<string> Summaries { get; } = ["sum", "min", "max", "avg", "count"];

    /// <summary>
    /// The date intervals a group may use (a number is a numeric interval).
    /// </summary>
    public static IReadOnlyList<string> Intervals { get; } =
        ["year", "quarter", "month", "day", "dayOfWeek", "hour", "minute", "second"];

    /// <summary>
    /// Checks the whole object; at the first error throws with the reason and the right form.
    /// </summary>
    public static void Validate<T>(DataSourceLoadOptionsBase options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var type = typeof(T);
        if (options.Filter is { Count: > 0 } filter)
            Filter(type, filter);
        foreach (var s in options.Sort ?? [])
            Field(type, s?.Selector, "Sort");
        foreach (var g in options.Group ?? [])
            Group(type, g);
        foreach (var r in options.TotalSummary ?? [])
            Summary(type, r, "TotalSummary");
        foreach (var r in options.GroupSummary ?? [])
            Summary(type, r, "GroupSummary");
        // "this" is the whole entity (DevExtreme): its reserved fields stay out of the JSON.
        foreach (var p in (options.Select ?? []).Where(p => p != "this"))
            Field(type, p, "Select", collectionAtEnd: true);
        foreach (var p in (options.PreSelect ?? []).Where(p => p != "this"))
            Field(type, p, "PreSelect", collectionAtEnd: true);
        if (options.DefaultSort is { } sort)
            Field(type, sort, "DefaultSort");
        foreach (var k in options.PrimaryKey ?? [])
            Field(type, k, "PrimaryKey");
    }

    // --- fields ---

    // The field at the end of the path. A collection is not entered; a collection at the end is allowed
    // only where collectionAtEnd (Select, PreSelect: read whole).
    private static PropertyInfo Field(Type type, string? path, string option, bool collectionAtEnd = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidRequestException($"In {option} the Selector is missing: {{\"Selector\": \"Field\", \"Desc\": false}}.");

        var segments = path.Split('.');
        PropertyInfo? property = null;
        for (var i = 0; i < segments.Length; i++)
        {
            property = type.GetProperty(segments[i], EntityPaths.PublicProperties)
                ?? throw new InvalidRequestException(NotFound(segments[i], type, option));

            var last = i == segments.Length - 1;
            if (IsCollection(property.PropertyType) && (!last || !collectionAtEnd))
                throw new InvalidRequestException(
                    $"'{property.Name}' is a collection of {EntityPaths.Inner(property.PropertyType).Name}: " +
                    "it is read whole with Select; it is not filtered, sorted, grouped or summed.");

            type = EntityPaths.Inner(property.PropertyType);
        }
        return property!;
    }

    private static string NotFound(string segment, Type type, string option)
    {
        var readable = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !EntityPaths.IsReserved(p))
            .ToList();
        var fields = readable.Where(p => !EntityPaths.IsNavigation(p.PropertyType)).Select(p => p.Name);
        var navigations = readable.Where(p => EntityPaths.IsNavigation(p.PropertyType)).Select(p => p.Name).ToList();
        var tail = navigations.Count > 0 ? $"; navigations: {string.Join(", ", navigations)}" : "";
        return $"The field '{segment}' does not exist on {type.Name} (in {option}). Fields: {string.Join(", ", fields)}{tail}.";
    }

    // --- the filter ---

    private static void Filter(Type type, IList node)
    {
        if (node.Count == 0)
            throw new InvalidRequestException(ConditionForm);

        switch (node[0])
        {
            case "!":
                if (node.Count != 2 || node[1] is not IList { Count: > 0 } negated)
                    throw new InvalidRequestException("After \"!\" goes a condition: [\"!\", [\"Quantity\", \">\", 2]].");
                Filter(type, negated);
                return;

            case string:
                Condition(type, node);
                return;

            case IList:
                foreach (var element in node)
                {
                    if (element is IList condition)
                        Filter(type, condition);
                    else if (element is string separator)
                    {
                        if (!separator.Equals("and", StringComparison.OrdinalIgnoreCase)
                            && !separator.Equals("or", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidRequestException(
                                $"'{separator}' does not join two conditions: between one condition and the next goes \"and\" or \"or\".");
                    }
                    else
                        throw new InvalidRequestException(ConditionForm);
                }
                return;

            default:
                throw new InvalidRequestException(ConditionForm);
        }
    }

    private static void Condition(Type type, IList triple)
    {
        if (triple.Count is not (2 or 3))
            throw new InvalidRequestException(ConditionForm);

        var property = Field(type, (string)triple[0]!, "Filter");
        var value = triple[triple.Count - 1];
        if (triple.Count == 3)
        {
            if (triple[1] is not string op)
                throw new InvalidRequestException(ConditionForm);
            var lower = op.ToLowerInvariant();
            if (!Operators.Contains(lower))
                throw new InvalidRequestException($"Unknown operator '{op}': use {string.Join(", ", Operators)}.");
            if (TextOperators.Contains(lower) && Underlying(property.PropertyType) != typeof(string))
                throw new InvalidRequestException($"{lower} works on text: '{property.Name}' is {Describe(property.PropertyType)}.");
        }
        Value(property, value);
    }

    // The value must convert to the type of the field as DevExtreme converts it: otherwise DevExtreme
    // would return 0 rows silently.
    private static void Value(PropertyInfo property, object? value)
    {
        var type = Underlying(property.PropertyType);
        var name = property.Name;

        if (value is null)
        {
            var nullable = !property.PropertyType.IsValueType || Nullable.GetUnderlyingType(property.PropertyType) is not null;
            if (!nullable)
                throw new InvalidRequestException($"'{name}' cannot be null: it is {Describe(property.PropertyType)}.");
            return;
        }

        if (value is IList)
            throw new InvalidRequestException(
                $"On '{name}' goes one value: for several values join the conditions with or, " +
                $"for example [[\"{name}\", \"=\", a], \"or\", [\"{name}\", \"=\", b]].");

        if (EntityPaths.IsNavigation(type))
            throw new InvalidRequestException($"'{name}' is a navigation: filter on one of its fields, for example \"{name}.Id\".");

        var text = value as string;
        var valid = type switch
        {
            _ when type == typeof(string) => value is string || IsNumber(value) || value is bool,
            _ when IsNumericType(type) => IsNumber(value)
                || (text is not null && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)),
            _ when type == typeof(bool) => value is bool || (text is not null && bool.TryParse(text, out _)),
            _ when type == typeof(DateOnly) => text is not null
                && DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ when type == typeof(DateTime) || type == typeof(DateTimeOffset) => text is not null
                && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
            _ when type.IsEnum => IsInteger(value)
                || (text is not null && Enum.TryParse(type, text, ignoreCase: true, out _)),
            _ when type == typeof(Guid) => text is not null && Guid.TryParse(text, out _),
            _ when type == typeof(TimeSpan) => text is not null && TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out _),
            _ => true,
        };
        if (!valid)
            throw new InvalidRequestException(WrongValue(name, type, value));
    }

    private static string WrongValue(string name, Type type, object value)
    {
        var written = value is string s ? $"\"{s}\"" : Convert.ToString(value, CultureInfo.InvariantCulture);
        if (type == typeof(DateOnly))
            return $"'{name}' is a date: it is written yyyy-MM-dd, for example \"2026-09-01\" ({written} is not).";
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return $"'{name}' is a date and time: it is written ISO 8601, for example \"2026-09-01T10:00:00Z\" or \"2026-09-01\" ({written} is not).";
        if (type.IsEnum)
            return $"'{name}' is one of: {string.Join(", ", Enum.GetNames(type))} ({written} is not).";
        if (type == typeof(bool))
            return $"'{name}' is a yes/no: use true or false ({written} is not).";
        return $"'{name}' is {Describe(type)}: {written} is not.";
    }

    // --- summaries and groups ---

    private static void Summary(Type type, SummaryInfo? summary, string option)
    {
        if (summary is null)
            return;
        var summaryType = summary.SummaryType?.ToLowerInvariant();
        if (summaryType is null || !Summaries.Contains(summaryType))
            throw new InvalidRequestException(
                $"Unknown summary '{summary.SummaryType}' (in {option}): use {string.Join(", ", Summaries)}.");

        // Measured: count without Selector counts the rows.
        if (summaryType == "count" && string.IsNullOrWhiteSpace(summary.Selector))
            return;

        var property = Field(type, summary.Selector, option);
        if (summaryType is "sum" or "avg" && !IsNumericType(Underlying(property.PropertyType)))
            throw new InvalidRequestException($"{summaryType} works on numbers: '{property.Name}' is {Describe(property.PropertyType)}.");
    }

    private static void Group(Type type, GroupingInfo? group)
    {
        var property = Field(type, group?.Selector, "Group");
        var interval = group!.GroupInterval;
        if (string.IsNullOrWhiteSpace(interval))
            return;

        var fieldType = Underlying(property.PropertyType);
        if (decimal.TryParse(interval, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
        {
            if (!IsNumericType(fieldType))
                throw new InvalidRequestException(
                    $"A numeric interval ({interval}) works on numbers: '{property.Name}' is {Describe(property.PropertyType)}.");
            // DevExtreme reads the width as a whole number: 12.5 would bucket by 12, a wrong answer taken as good; 0 divides by zero.
            if (width != decimal.Truncate(width) || width <= 0 || width > int.MaxValue)
                throw new InvalidRequestException($"A numeric interval is a whole number above zero: {interval} is not.");
            return;
        }

        var known = Intervals.FirstOrDefault(i => i.Equals(interval, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidRequestException($"Unknown interval '{interval}': use {string.Join(", ", Intervals)}, or a number.");

        var withTime = fieldType == typeof(DateTime) || fieldType == typeof(DateTimeOffset);
        if (!withTime && fieldType != typeof(DateOnly))
            throw new InvalidRequestException($"The interval '{known}' works on dates: '{property.Name}' is {Describe(property.PropertyType)}.");
        if (!withTime && IntervalsWithTime.Contains(known))
            throw new InvalidRequestException($"'{property.Name}' is a date without time: hour, minute and second are not there.");
    }

    // --- the types ---

    private static Type Underlying(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private static bool IsCollection(Type type) => type != typeof(string) && EntityPaths.Inner(type) != type;

    private static bool IsNumericType(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
        || type == typeof(decimal) || type == typeof(double) || type == typeof(float);

    private static bool IsNumber(object value) => value is int or long or short or byte or decimal or double or float;

    private static bool IsInteger(object value) =>
        value is int or long or short or byte || (value is decimal d && decimal.Truncate(d) == d);

    private static string Describe(Type type)
    {
        var t = Underlying(type);
        if (IsNumericType(t))
            return "a number";
        if (t == typeof(bool))
            return "a yes/no";
        if (t == typeof(DateOnly))
            return "a date";
        if (t == typeof(DateTime) || t == typeof(DateTimeOffset))
            return "a date and time";
        if (t.IsEnum)
            return $"one of {string.Join(", ", Enum.GetNames(t))}";
        if (t == typeof(Guid))
            return "a Guid";
        if (t == typeof(string))
            return "a text";
        return $"a {t.Name}";
    }
}
