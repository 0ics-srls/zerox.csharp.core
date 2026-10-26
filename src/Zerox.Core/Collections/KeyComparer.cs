namespace Zerox.Core.Collections;

// Orders key/value pairs by key (a buffer of points sorted by key is searched with it).
public static class KeyComparer
{
    public static IComparer<KeyValuePair<TKey, TValue>> For<TKey, TValue>()
        where TKey : IComparable<TKey>
        => Cache<TKey, TValue>.Instance;

    private static class Cache<TKey, TValue>
        where TKey : IComparable<TKey>
    {
        public static readonly IComparer<KeyValuePair<TKey, TValue>> Instance =
            Comparer<KeyValuePair<TKey, TValue>>.Create(static (left, right) => left.Key.CompareTo(right.Key));
    }
}
