using System.Text.Json.Serialization;
using Zerox.Ef.Core;

namespace Zerox.Ef.Api.Tests.Sample.Db;

public sealed class Customer : IEntity, IHasOptimisticLock
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string City { get; set; } = "";

    public int Version { get; set; }

    // Breaks the JSON cycle; still a navigation, so still usable in a Select.
    [JsonIgnore]
    public List<Order> Orders { get; set; } = [];
}
