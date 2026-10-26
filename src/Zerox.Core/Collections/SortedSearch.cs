namespace Zerox.Core.Collections;

// Searches over a sorted sequence (ascending for the comparer). One implementation for arrays,
// spans and lists (a List<T> through CollectionsMarshal.AsSpan).
public static class SortedSearch
{
    // Index of the first element not less than value; Length when there is none.
    public static int LowerBound<T>(this ReadOnlySpan<T> sorted, T value, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        int low = 0, high = sorted.Length;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (comparer.Compare(sorted[middle], value) < 0)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    // Index of the first element greater than value; Length when there is none.
    public static int UpperBound<T>(this ReadOnlySpan<T> sorted, T value, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        int low = 0, high = sorted.Length;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (comparer.Compare(sorted[middle], value) <= 0)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    // Index of the first element equal to value, or the bitwise complement of where it would go.
    public static int BinarySearchFirst<T>(this ReadOnlySpan<T> sorted, T value, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        var index = sorted.LowerBound(value, comparer);
        return index < sorted.Length && comparer.Compare(sorted[index], value) == 0 ? index : ~index;
    }

    // Index of the last element equal to value, or the bitwise complement of where it would go.
    public static int BinarySearchLast<T>(this ReadOnlySpan<T> sorted, T value, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        var index = sorted.UpperBound(value, comparer);
        return index > 0 && comparer.Compare(sorted[index - 1], value) == 0 ? index - 1 : ~index;
    }
}
