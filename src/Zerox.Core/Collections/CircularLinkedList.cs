using System.Collections;

namespace Zerox.Core.Collections;

// Unique items in insertion order; Next() walks round and round. Add, Remove and Contains are O(1).
public sealed class CircularLinkedList<T> : IEnumerable<T>
    where T : class
{
    private readonly Dictionary<T, Node> _index = [];
    private Node? _head;
    private Node? _current;

    public int Count => _index.Count;

    public bool Contains(T item) => _index.ContainsKey(item);

    public void Add(T item)
    {
        if (!TryAdd(item))
            throw new ArgumentException("The item is already in the list", nameof(item));
    }

    public bool TryAdd(T item)
    {
        var node = new Node(item);
        if (!_index.TryAdd(item, node))
            return false;
        if (_head is null)
        {
            node.Next = node;
            node.Previous = node;
            _head = _current = node;
        }
        else
        {
            node.Next = _head;
            node.Previous = _head.Previous;
            _head.Previous!.Next = node;
            _head.Previous = node;
        }
        return true;
    }

    public bool Remove(T item)
    {
        if (!_index.Remove(item, out var node))
            return false;
        if (_index.Count == 0)
        {
            _head = _current = null;
            return true;
        }
        if (_current == node)
            _current = node.Next;
        if (_head == node)
            _head = node.Next;
        node.Next!.Previous = node.Previous;
        node.Previous!.Next = node.Next;
        return true;
    }

    public void Clear()
    {
        _head = _current = null;
        _index.Clear();
    }

    public T? Peek() => _current?.Value;

    // The current item; the cursor moves to the next one.
    public T? Next()
    {
        if (_current is null)
            return null;
        var value = _current.Value;
        _current = _current.Next;
        return value;
    }

    public void Reset() => _current = _head;

    // From the head, once round.
    public IEnumerator<T> GetEnumerator()
    {
        if (_head is null)
            yield break;
        var node = _head;
        do
        {
            yield return node.Value;
            node = node.Next!;
        }
        while (node != _head);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class Node(T value)
    {
        public T Value { get; } = value;
        public Node? Previous { get; set; }
        public Node? Next { get; set; }
    }
}
