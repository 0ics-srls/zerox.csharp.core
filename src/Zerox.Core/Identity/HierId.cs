using System.Collections.Immutable;

namespace Zerox.Core.Identity;

// An id made of parts: P1/P2/P3. Immutable: every operation returns a new id.
// Compare with null through "is null": with two == operators (id and string) "== null" is ambiguous.
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

    // The last part: what the id names inside its parent; "" for an empty id.
    public string Name => Count == 0 ? string.Empty : _parts[^1];

    // The id without its last part; an id of one part, and an empty one, has the empty id as its parent.
    public HierId Parent => Count <= 1 ? Empty(Separator) : new(_parts.Take(Count - 1), Separator);

    public static HierId Empty(char separator = DefaultSeparator) => new([], separator);

    public static HierId From(params string[] parts) => new(parts, DefaultSeparator);

    public static HierId From(IEnumerable<string> parts, char separator = DefaultSeparator) => new(parts, separator);

    public static HierId From(HierId from)
    {
        ArgumentNullException.ThrowIfNull(from);
        return new(from._parts, from.Separator);
    }

    public static HierId From(HierId from, char separator)
    {
        ArgumentNullException.ThrowIfNull(from);
        return new(from._parts, separator);
    }

    public static HierId Parse(string text, char separator = DefaultSeparator)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == 0 ? Empty(separator) : new(text.Split(separator), separator);
    }

    // The part at index; beyond the last part, the last part; an empty id gives "".
    public string GetPart(int index) => Count == 0 ? string.Empty : _parts[Math.Min(index, Count - 1)];

    // The first count parts; the id itself when count is not between 1 and Count.
    public HierId GetParts(int count) => count > 0 && count <= Count ? new(_parts.Take(count), Separator) : this;

    // Without the first count parts: the id itself for count ≤ 0, empty for count ≥ Count.
    public HierId Splice(int count = 1)
        => count <= 0 ? this : count >= Count ? Empty(Separator) : new(_parts.Skip(count), Separator);

    public HierId CopyWithNewPrefix(HierId prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return prefix.Concat(Splice(prefix.Count));
    }

    public HierId Append(string part) => new([.. _parts, part], Separator);

    // Without the first part equal to part.
    public HierId Remove(string part)
    {
        var index = _parts.IndexOf(part, StringComparer.Ordinal);
        return index < 0 ? this : new(_parts.RemoveAt(index), Separator);
    }

    public HierId Concat(HierId other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new([.. _parts, .. other._parts], Separator);
    }

    // Number of leading parts equal in both ids.
    public int GetSimilarRootSize(HierId other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var end = Math.Min(Count, other.Count);
        for (var i = 0; i < end; ++i)
        {
            if (!string.Equals(_parts[i], other._parts[i], StringComparison.Ordinal))
                return i;
        }
        return end;
    }

    // Number of trailing parts equal in both ids.
    public int GetNumberOfEndingMatchingParts(HierId other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var matching = 0;
        for (int i = Count - 1, j = other.Count - 1; i >= 0 && j >= 0; --i, --j)
        {
            if (!string.Equals(_parts[i], other._parts[j], StringComparison.Ordinal))
                break;
            ++matching;
        }
        return matching;
    }

    // The parts where pattern is true must be equal; the comparison stops at the shortest of the three.
    public bool MatchPartsByPattern(HierId other, IEnumerable<bool> pattern)
    {
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(pattern);
        var index = 0;
        foreach (var mustMatch in pattern)
        {
            if (index >= Count || index >= other.Count)
                break;
            if (mustMatch && !string.Equals(_parts[index], other._parts[index], StringComparison.Ordinal))
                return false;
            ++index;
        }
        return true;
    }

    public bool MatchPartsByPatternPms(HierId other, params bool[] pattern) => MatchPartsByPattern(other, pattern);

    public bool StartsWith(HierId prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return GetSimilarRootSize(prefix) == prefix.Count;
    }

    public static HierId operator +(HierId left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.Append(right);
    }

    public static HierId operator +(HierId left, HierId right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.Concat(right);
    }

    public static HierId operator -(HierId left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.Remove(right);
    }

    public static bool operator ==(HierId? left, HierId? right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(HierId? left, HierId? right) => !(left == right);

    public static bool operator ==(HierId? left, string? right) => left?.ToString() == right;

    public static bool operator !=(HierId? left, string? right) => !(left == right);

    public bool Equals(HierId? other)
        => other is not null && Separator == other.Separator && _parts.AsSpan().SequenceEqual(other._parts.AsSpan());

    public override bool Equals(object? obj) => obj is HierId other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Separator);
        foreach (var part in _parts)
            hash.Add(part, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    public override string ToString() => string.Join(Separator, _parts);

    // The first levels parts; all of them when levels ≤ 0.
    public string ToString(int levels) => string.Join(Separator, _parts.Take(levels > 0 ? Math.Min(levels, Count) : Count));
}
