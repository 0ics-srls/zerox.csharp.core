using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zerox.Ef.Api.Tests.Sample.Api;

namespace Zerox.Ef.Api.Tests.Sample;

// End to end over HTTP on the example application: every action of EntityController<T>.
public sealed class EntityControllerApiTests : IAsyncLifetime
{
    private SampleApp? _app;

    private SampleApp App => _app ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _app = await SampleApp.StartAsync();
        await App.SeedAsync();
    }

    public async ValueTask DisposeAsync() => await App.DisposeAsync();

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(HttpMethod method, string url, string? json = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = json is null ? null : Json(json) };
        using var response = await App.Client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        if (string.IsNullOrEmpty(text))
            return (response.StatusCode, default);
        using var document = JsonDocument.Parse(text);
        return (response.StatusCode, document.RootElement.Clone());
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(string url, string json) => SendAsync(HttpMethod.Post, url, json);

    private static List<JsonElement> Data(JsonElement body) => body.GetProperty("data").EnumerateArray().ToList();

    // --- query: the DevExtreme load options, server-side ---

    [Fact]
    public async Task Should_PageTheRows_WhenQueryHasSkipAndTake()
    {
        var (status, body) = await PostAsync("api/Orders/query", """{"Skip":10,"Take":5,"RequireTotalCount":true}""");

        status.Should().Be(HttpStatusCode.OK);
        Data(body).Should().HaveCount(5);
        body.GetProperty("totalCount").GetInt32().Should().Be(25);
    }

    [Fact]
    public async Task Should_FilterOnTheServer_WhenQueryHasAFilter()
    {
        var (_, closed) = await PostAsync("api/Orders/query", """{"Filter":["Status","=","Closed"]}""");
        var (_, cargo) = await PostAsync("api/Orders/query", """{"Filter":["Customer.Name","contains","a"],"RequireTotalCount":true}""");

        Data(closed).Should().HaveCount(5).And.OnlyContain(o => o.GetProperty("Status").GetString() == "Closed");
        cargo.GetProperty("totalCount").GetInt32().Should().Be(8);   // only Cargo has a lower-case "a"
    }

    [Fact]
    public async Task Should_SortOnTheServer_WhenQueryHasSort()
    {
        var (_, body) = await PostAsync("api/Orders/query", """{"Sort":[{"Selector":"Amount","Desc":true}],"Take":3}""");

        Data(body).Select(o => o.GetProperty("Amount").GetDecimal()).Should().Equal(250m, 240m, 230m);
    }

    [Fact]
    public async Task Should_ReturnEverything_WhenTheQueryBodyIsNull()
    {
        var (status, body) = await PostAsync("api/Orders/query", "null");

        status.Should().Be(HttpStatusCode.OK);
        Data(body).Should().HaveCount(25);
    }

    [Fact]
    public async Task Should_AcceptCamelCaseOptionKeys()
    {
        var (_, camel) = await PostAsync("api/Orders/query", """{"skip":0,"take":2}""");
        var (_, pascal) = await PostAsync("api/Orders/query", """{"Skip":0,"Take":2}""");

        Data(camel).Should().HaveCount(2);
        Data(camel).Select(o => o.GetProperty("Id").GetInt32()).Should().Equal(Data(pascal).Select(o => o.GetProperty("Id").GetInt32()));
    }

    [Fact]
    public async Task Should_ReturnTheTotalSummary_WhenAsked()
    {
        var (_, body) = await PostAsync("api/Orders/query", """{"TotalSummary":[{"Selector":"Amount","SummaryType":"sum"}],"Take":1}""");

        body.GetProperty("summary")[0].GetDecimal().Should().Be(3250m);
    }

    [Fact]
    public async Task Should_Answer400WithTheReason_WhenTheFilterIsWrong()
    {
        var (status, body) = await PostAsync("api/Orders/query", """{"Filter":["Nope","=",1]}""");

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("Code").GetString().Should().Be("InvalidRequest");
        body.GetProperty("Message").GetString().Should().Contain("'Nope'");
    }

    [Fact]
    public async Task Should_Answer400_WhenAReservedFieldIsNamed()
    {
        var (status, body) = await PostAsync("api/Orders/query", """{"Filter":["InternalNote","=","internal"]}""");

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("Message").GetString().Should().Contain("InternalNote");
    }

    // --- groups, projection, values ---

    [Fact]
    public async Task Should_ReturnGroups_WhenGroupsIsCalled()
    {
        var (status, body) = await PostAsync("api/Orders/groups",
            """{"Group":[{"Selector":"Status"}],"GroupSummary":[{"Selector":"Amount","SummaryType":"sum"}]}""");

        status.Should().Be(HttpStatusCode.OK);
        var groups = body.GetProperty("Groups").EnumerateArray()
            .ToDictionary(g => g.GetProperty("Key").ToString(), g => (Count: g.GetProperty("Count").GetInt32(), Sum: g.GetProperty("Summary")[0].GetDecimal()));
        groups.Should().HaveCount(2);
        groups.Values.Sum(g => g.Count).Should().Be(25);
        groups.Values.Select(g => g.Sum).Should().BeEquivalentTo([750m, 2500m]);
    }

    [Fact]
    public async Task Should_ReturnNamesInsteadOfIds_WhenProjectionIsCalled()
    {
        var (status, body) = await PostAsync("api/Orders/projection",
            """{"Select":["Amount","Customer.Name"],"Sort":[{"Selector":"Amount"}],"Take":1}""");

        status.Should().Be(HttpStatusCode.OK);
        var row = Data(body).Single();
        row.GetProperty("Amount").GetDecimal().Should().Be(10m);
        row.GetProperty("Customer").GetProperty("Name").GetString().Should().Be("Bolt");   // order 1 → customer 1
        row.TryGetProperty("CustomerId", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReturnDistinctCities_WhenValuesIsCalled()
    {
        var (status, body) = await SendAsync(HttpMethod.Get, "api/Customers/values/City");

        status.Should().Be(HttpStatusCode.OK);
        body.EnumerateArray().Select(v => v.GetString()).Should().Equal("Milan", "Rome");
    }

    [Theory]
    [InlineData("Version")]
    [InlineData("Nope")]
    public async Task Should_Answer400_WhenValuesNamesAColumnThatIsNotText(string column)
    {
        var (status, body) = await SendAsync(HttpMethod.Get, $"api/Customers/values/{column}");

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("Code").GetString().Should().Be("InvalidRequest");
    }

    // --- CRUD ---

    [Fact]
    public async Task Should_Create_WhenPosted()
    {
        using var response = await App.Client.PostAsync("api/Customers", Json("""{"Name":"Delta","City":"Turin"}"""), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var location = response.Headers.Location!.ToString();
        location.Should().MatchRegex(@"/api/Customers/\d+$");

        var (status, body) = await SendAsync(HttpMethod.Get, location);
        status.Should().Be(HttpStatusCode.OK);
        body.GetProperty("Name").GetString().Should().Be("Delta");
        body.GetProperty("City").GetString().Should().Be("Turin");
    }

    [Fact]
    public async Task Should_Answer404_WhenGettingAMissingId()
    {
        var (status, _) = await SendAsync(HttpMethod.Get, "api/Customers/404");

        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_Update_WhenPut()
    {
        var (_, customer) = await SendAsync(HttpMethod.Get, "api/Customers/1");
        var id = customer.GetProperty("Id").GetInt32();

        var (status, _) = await SendAsync(HttpMethod.Put, $"api/Customers/{id}",
            $$"""{"Id":{{id}},"Name":"Acme Ltd","City":"Rome","Version":{{customer.GetProperty("Version").GetInt32()}}}""");

        status.Should().Be(HttpStatusCode.OK);
        await using var check = App.NewContext();
        var saved = await check.Customers.SingleAsync(c => c.Id == id, Ct);
        saved.Name.Should().Be("Acme Ltd");
        saved.Version.Should().Be(1);
    }

    [Fact]
    public async Task Should_Answer400_WhenPutIdsDiffer()
    {
        var (status, body) = await SendAsync(HttpMethod.Put, "api/Customers/1", """{"Id":2,"Name":"x","City":"y","Version":0}""");

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("Code").GetString().Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task Should_Delete_WhenDeleted()
    {
        using var created = await App.Client.PostAsync("api/Customers", Json("""{"Name":"Temp","City":"Bari"}"""), Ct);
        var location = created.Headers.Location!.ToString();

        (await SendAsync(HttpMethod.Delete, location)).Status.Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Get, location)).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_Answer409_WhenPutCarriesAStaleVersion()
    {
        // Both clients read the customer at Version 0; the first saves, the second is stale.
        var first = await SendAsync(HttpMethod.Put, "api/Customers/2", """{"Id":2,"Name":"Bolt One","City":"Milan","Version":0}""");
        var second = await SendAsync(HttpMethod.Put, "api/Customers/2", """{"Id":2,"Name":"Bolt Two","City":"Milan","Version":0}""");

        first.Status.Should().Be(HttpStatusCode.OK);
        second.Status.Should().Be(HttpStatusCode.Conflict);
        second.Body.GetProperty("Code").GetString().Should().Be("Conflict");
        await using var check = App.NewContext();
        (await check.Customers.SingleAsync(c => c.Id == 2, Ct)).Name.Should().Be("Bolt One");
    }
}
