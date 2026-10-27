using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zerox.Ef.Api.Tests.Sample.Api;
using Zerox.Ef.Api.Tests.Sample.Db;

namespace Zerox.Ef.Api.Tests.Sample;

// The example end to end over HTTP: merge (unit of work), close before a date (massive operation),
// rename (last writer wins) — and the whole lifecycle in one go.
public sealed class SampleApiTests : IAsyncLifetime
{
    private const int Acme = 1;
    private const int Bolt = 2;

    private SampleApp? _app;

    private SampleApp App => _app ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _app = await SampleApp.StartAsync();
        await App.SeedAsync();
    }

    public async ValueTask DisposeAsync() => await App.DisposeAsync();

    private async Task<(HttpStatusCode Status, JsonElement Body, Uri? Location)> SendAsync(HttpMethod method, string url, string? json = null)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json"),
        };
        using var response = await App.Client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        var body = default(JsonElement);
        if (!string.IsNullOrEmpty(text))
        {
            using var document = JsonDocument.Parse(text);
            body = document.RootElement.Clone();
        }
        return (response.StatusCode, body, response.Headers.Location);
    }

    [Fact]
    public async Task Should_Answer204AndMerge_WhenMergeIsCalledOverHttp()
    {
        var (status, _, _) = await SendAsync(HttpMethod.Post, $"api/Customers/{Bolt}/merge-into/{Acme}");

        status.Should().Be(HttpStatusCode.NoContent);
        await using var check = App.NewContext();
        (await check.Customers.AnyAsync(c => c.Id == Bolt, Ct)).Should().BeFalse();
        (await check.Orders.CountAsync(o => o.CustomerId == Acme, Ct)).Should().Be(17);
    }

    [Fact]
    public async Task Should_Answer404_WhenMergeNamesAMissingCustomer()
    {
        (await SendAsync(HttpMethod.Post, $"api/Customers/{Bolt}/merge-into/404")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_CloseOnlyOpenOrdersBeforeTheDate_WhenCloseBeforeIsCalled()
    {
        // Orders 1..10 are dated 2026-01-02..2026-01-11; 5 and 10 are already closed.
        var (status, body, _) = await SendAsync(HttpMethod.Post, "api/Orders/close-before/2026-01-12");

        status.Should().Be(HttpStatusCode.OK);
        body.GetProperty("Closed").GetInt32().Should().Be(8);
        await using var check = App.NewContext();
        (await check.Orders.CountAsync(o => o.Date < new DateOnly(2026, 1, 12) && o.Status == OrderStatus.Open, Ct)).Should().Be(0);
        (await check.Orders.CountAsync(o => o.Date >= new DateOnly(2026, 1, 12) && o.Status == OrderStatus.Closed, Ct)).Should().Be(3);   // 15, 20, 25
    }

    [Fact]
    public async Task Should_Answer409_WhenARenameCarriesAStaleVersion()
    {
        // Both clients read Acme at Version 0: the first renames, the second decided on a stale row.
        var a = await SendAsync(HttpMethod.Put, $"api/Customers/{Acme}/name", """{"Name":"Acme by A","Version":0}""");
        var b = await SendAsync(HttpMethod.Put, $"api/Customers/{Acme}/name", """{"Name":"Acme by B","Version":0}""");

        a.Status.Should().Be(HttpStatusCode.OK);
        b.Status.Should().Be(HttpStatusCode.Conflict);
        b.Body.GetProperty("Code").GetString().Should().Be("Conflict");
        await using var check = App.NewContext();
        var saved = await check.Customers.SingleAsync(c => c.Id == Acme, Ct);
        saved.Name.Should().Be("Acme by A");
        saved.Version.Should().Be(1);
    }

    [Fact]
    public async Task Should_Answer404_WhenRenamingAMissingCustomer()
    {
        (await SendAsync(HttpMethod.Put, "api/Customers/404/name", """{"Name":"x","Version":0}""")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_Answer409_WhenAFullPutOrARenameIsStale()
    {
        await SendAsync(HttpMethod.Put, $"api/Customers/{Acme}/name", """{"Name":"Renamed","Version":0}""");   // now at 1

        var stalePut = await SendAsync(HttpMethod.Put, $"api/Customers/{Acme}", $$"""{"Id":{{Acme}},"Name":"Whole","City":"Rome","Version":0}""");
        var staleRename = await SendAsync(HttpMethod.Put, $"api/Customers/{Acme}/name", """{"Name":"Wins","Version":0}""");

        stalePut.Status.Should().Be(HttpStatusCode.Conflict);   // the whole entity would overwrite somebody's work
        staleRename.Status.Should().Be(HttpStatusCode.Conflict); // one field: still decided on a stale row
    }

    [Fact]
    public async Task Should_ShowTheWholeLifecycle()
    {
        // Create two customers and their orders.
        var north = await SendAsync(HttpMethod.Post, "api/Customers", """{"Name":"North","City":"Turin"}""");
        var south = await SendAsync(HttpMethod.Post, "api/Customers", """{"Name":"South","City":"Bari"}""");
        north.Status.Should().Be(HttpStatusCode.Created);
        south.Status.Should().Be(HttpStatusCode.Created);
        var northId = north.Body.GetProperty("Id").GetInt32();
        var southId = south.Body.GetProperty("Id").GetInt32();
        foreach (var (customer, amount, date) in new[] { (northId, 100, "2026-03-01"), (northId, 200, "2026-03-02"), (southId, 300, "2026-03-03") })
            (await SendAsync(HttpMethod.Post, "api/Orders", $$"""{"CustomerId":{{customer}},"Date":"{{date}}","Amount":{{amount}},"Status":"Open"}""")).Status
                .Should().Be(HttpStatusCode.Created);

        // Query with a filter and paging.
        var page = await SendAsync(HttpMethod.Post, "api/Orders/query",
            $$"""{"Filter":["CustomerId","=",{{northId}}],"Sort":[{"Selector":"Amount"}],"Take":1,"RequireTotalCount":true}""");
        page.Body.GetProperty("totalCount").GetInt32().Should().Be(2);
        page.Body.GetProperty("data")[0].GetProperty("Amount").GetDecimal().Should().Be(100m);

        // Project names instead of ids, and sum per customer.
        var names = await SendAsync(HttpMethod.Post, "api/Orders/projection",
            """{"Select":["Amount","Customer.Name"],"Filter":["Date",">=","2026-03-01"],"Sort":[{"Selector":"Amount","Desc":true}]}""");
        names.Body.GetProperty("data")[0].GetProperty("Customer").GetProperty("Name").GetString().Should().Be("South");
        var sums = await SendAsync(HttpMethod.Post, "api/Orders/groups",
            """{"Filter":["Date",">=","2026-03-01"],"Group":[{"Selector":"Customer.Name"}],"GroupSummary":[{"Selector":"Amount","SummaryType":"sum"}]}""");
        sums.Body.GetProperty("Groups").EnumerateArray()
            .ToDictionary(g => g.GetProperty("Key").GetString()!, g => g.GetProperty("Summary")[0].GetDecimal())
            .Should().BeEquivalentTo(new Dictionary<string, decimal> { ["North"] = 300m, ["South"] = 300m });

        // Merge South into North, close everything before April, rename North, delete an order.
        (await SendAsync(HttpMethod.Post, $"api/Customers/{southId}/merge-into/{northId}")).Status.Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Post, "api/Orders/close-before/2026-04-01")).Body.GetProperty("Closed").GetInt32().Should().Be(23);
        (await SendAsync(HttpMethod.Put, $"api/Customers/{northId}/name", """{"Name":"North & South","Version":0}""")).Status.Should().Be(HttpStatusCode.OK);
        var last = page.Body.GetProperty("data")[0].GetProperty("Id").GetInt32();
        (await SendAsync(HttpMethod.Delete, $"api/Orders/{last}")).Status.Should().Be(HttpStatusCode.NoContent);

        // The database at the end.
        await using var check = App.NewContext();
        (await check.Customers.SingleAsync(c => c.Id == northId, Ct)).Name.Should().Be("North & South");
        (await check.Customers.AnyAsync(c => c.Id == southId, Ct)).Should().BeFalse();
        (await check.Orders.Where(o => o.CustomerId == northId).Select(o => o.Amount).ToListAsync(Ct)).Should().BeEquivalentTo([200m, 300m]);
        (await check.Orders.CountAsync(o => o.Status == OrderStatus.Open, Ct)).Should().Be(0);
    }
}
