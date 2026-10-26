namespace Zerox.Core.Statistics;

// Marks a numeric property of an IAggregableSource as a series the system creates and feeds.
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class AggregableAttribute : Attribute
{
    // The series name part; the property name when null.
    public string? Name { get; set; }

    // A group the series joins (a prefix group of that name is registered).
    public string? Group { get; set; }

    // #RRGGBB or #RRGGBBAA, read by the charts.
    public string? Color { get; set; }
}
