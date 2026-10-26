using System.ComponentModel;

namespace Zerox.Core.Statistics;

// A service whose [Aggregable] properties become series; a property change samples it.
public interface IAggregableSource : INotifyPropertyChanged
{
    string Name { get; }
}
