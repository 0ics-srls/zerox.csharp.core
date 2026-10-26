using Zerox.Core.Identity;

namespace Zerox.Core.Statistics;

// A named set of series, by their ids: a list of exact ids or an id prefix.
public sealed class SeriesGroup
{
    private readonly Predicate<HierId> _contains;

    private SeriesGroup(string name, Predicate<HierId> contains)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        _contains = contains;
    }

    public string Name { get; }

    public bool Contains(HierId id) => _contains(id);

    public static SeriesGroup Of(string name, params HierId[] members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var set = members.ToHashSet();
        return new(name, set.Contains);
    }

    public static SeriesGroup WithPrefix(string name, HierId prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return new(name, id => id.StartsWith(prefix));
    }
}
