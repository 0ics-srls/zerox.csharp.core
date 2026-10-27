using System.Text.Json.Serialization;
using Zerox.Ef.Core;

namespace Zerox.Ef.Api.Tests.Sample.Db;

public enum OrderStatus
{
    Open,
    Closed,
}

public sealed class Order : IEntity
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public DateOnly Date { get; set; }

    public decimal Amount { get; set; }

    public OrderStatus Status { get; set; }

    // A reserved field: never in a response, never named in a search.
    [JsonIgnore]
    public string? InternalNote { get; set; }
}
