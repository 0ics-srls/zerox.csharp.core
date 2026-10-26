using System.Numerics;

namespace Zerox.Core.Collections;

// A mask of any number of bits. Bits outside Size are never set, so the words compare as they are.
public sealed class BitMask : IEquatable<BitMask>
{
    private readonly ulong[] _words;

    public BitMask(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        Size = size;
        _words = new ulong[(size + 63) / 64];
    }

    public int Size { get; }

    public int Count
    {
        get
        {
            var count = 0;
            foreach (var word in _words)
                count += BitOperations.PopCount(word);
            return count;
        }
    }

    public bool Get(int index)
    {
        CheckIndex(index);
        return (_words[index >> 6] & (1UL << (index & 63))) != 0;
    }

    public void Set(int index)
    {
        CheckIndex(index);
        _words[index >> 6] |= 1UL << (index & 63);
    }

    public void Reset(int index)
    {
        CheckIndex(index);
        _words[index >> 6] &= ~(1UL << (index & 63));
    }

    // Ascending indexes of the set bits.
    public IReadOnlyList<int> GetSetBits()
    {
        var bits = new List<int>(Count);
        for (var word = 0; word < _words.Length; word++)
        {
            var remaining = _words[word];
            while (remaining != 0)
            {
                bits.Add((word << 6) + BitOperations.TrailingZeroCount(remaining));
                remaining &= remaining - 1;
            }
        }
        return bits;
    }

    public static BitMask operator |(BitMask left, BitMask right) => BitwiseOr(left, right);

    public static BitMask operator &(BitMask left, BitMask right) => BitwiseAnd(left, right);

    public static bool operator ==(BitMask? left, BitMask? right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(BitMask? left, BitMask? right) => !(left == right);

    public static BitMask BitwiseOr(BitMask left, BitMask right) => Combine(left, right, static (a, b) => a | b);

    public static BitMask BitwiseAnd(BitMask left, BitMask right) => Combine(left, right, static (a, b) => a & b);

    public bool Equals(BitMask? other)
        => other is not null && Size == other.Size && _words.AsSpan().SequenceEqual(other._words);

    public override bool Equals(object? obj) => obj is BitMask other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Size);
        foreach (var word in _words)
            hash.Add(word);
        return hash.ToHashCode();
    }

    private void CheckIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Size);
    }

    private static BitMask Combine(BitMask left, BitMask right, Func<ulong, ulong, ulong> operation)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Size != right.Size)
            throw new ArgumentException($"Masks of different sizes: {left.Size} and {right.Size}", nameof(right));
        var result = new BitMask(left.Size);
        for (var i = 0; i < left._words.Length; i++)
            result._words[i] = operation(left._words[i], right._words[i]);
        return result;
    }
}
