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
