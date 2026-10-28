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

    // --- load: the DevExtreme load options, server-side; the options decide rows, columns or groups ---

    [Fact]
    public async Task Should_PageTheRows_WhenLoadHasSkipAndTake()
    {
        var (status, body) = await PostAsync("api/Orders/load", """{"Skip":10,"Take":5,"RequireTotalCount":true}""");

        status.Should().Be(HttpStatusCode.OK);
        Data(body).Should().HaveCount(5);
        body.GetProperty("totalCount").GetInt32().Should().Be(25);
    }

    [Fact]
    public async Task Should_FilterOnTheServer_WhenLoadHasAFilter()
    {
        var (_, closed) = await PostAsync("api/Orders/load", """{"Filter":["Status","=","Closed"],"Take":100}""");
        var (_, cargo) = await PostAsync("api/Orders/load", """{"Filter":["Customer.Name","contains","a"],"RequireTotalCount":true,"Take":100}""");

        Data(closed).Should().HaveCount(5).And.OnlyContain(o => o.GetProperty("Status").GetString() == "Closed");
        cargo.GetProperty("totalCount").GetInt32().Should().Be(8);   // only Cargo has a lower-case "a"
    }

    [Fact]
    public async Task Should_SortOnTheServer_WhenLoadHasSort()
    {
        var (_, body) = await PostAsync("api/Orders/load", """{"Sort":[{"Selector":"Amount","Desc":true}],"Take":3}""");

        Data(body).Select(o => o.GetProperty("Amount").GetDecimal()).Should().Equal(250m, 240m, 230m);
    }

    [Fact]
    public async Task Should_Answer400_WhenTheLoadBodyIsNull()
    {
        var (status, body) = await PostAsync("api/Orders/load", "null");

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("detail").GetString().Should().Contain("Take");
    }

    [Theory]
    [InlineData("""{"Skip":0}""")]
    [InlineData("""{"Take":1001}""")]
    public async Task Should_Answer400_WhenRowsAreAskedWithoutAPage(string json)
    {
        var (status, body) = await PostAsync("api/Orders/load", json);

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("detail").GetString().Should().Contain("Take");
    }

    [Fact]
    public async Task Should_CountWithoutAPage_WhenOnlyTheCountIsAsked()
    {
        var (status, body) = await PostAsync("api/Orders/load", """{"IsCountQuery":true}""");

        status.Should().Be(HttpStatusCode.OK);
        body.GetProperty("totalCount").GetInt32().Should().Be(25);
    }

    [Fact]
    public async Task Should_AcceptCamelCaseOptionKeys()
    {
        var (_, camel) = await PostAsync("api/Orders/load", """{"skip":0,"take":2}""");
        var (_, pascal) = await PostAsync("api/Orders/load", """{"Skip":0,"Take":2}""");

        Data(camel).Should().HaveCount(2);
        Data(camel).Select(o => o.GetProperty("Id").GetInt32()).Should().Equal(Data(pascal).Select(o => o.GetProperty("Id").GetInt32()));
    }

    [Fact]
    public async Task Should_ReturnTheTotalSummary_WhenAsked()
    {
        var (_, body) = await PostAsync("api/Orders/load", """{"TotalSummary":[{"Selector":"Amount","SummaryType":"sum"}],"Take":1}""");

        body.GetProperty("summary")[0].GetDecimal().Should().Be(3250m);
    }

    [Fact]
    public async Task Should_Answer400WithTheReason_WhenTheFilterIsWrong()
    {
        var (status, body) = await PostAsync("api/Orders/load", """{"Filter":["Nope","=",1]}""");

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("Code").GetString().Should().Be("InvalidRequest");
        body.GetProperty("detail").GetString().Should().Contain("'Nope'");
    }

    [Fact]
    public async Task Should_Answer400_WhenAReservedFieldIsNamed()
    {
        var (status, body) = await PostAsync("api/Orders/load", """{"Filter":["InternalNote","=","internal"]}""");

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("detail").GetString().Should().Contain("InternalNote");
    }

    // --- groups, columns, values ---

    [Fact]
    public async Task Should_ReturnGroupsWithoutRows_WhenLoadHasGroup()
    {
        var (status, body) = await PostAsync("api/Orders/load",
            """{"Group":[{"Selector":"Status","IsExpanded":true}],"GroupSummary":[{"Selector":"Amount","SummaryType":"sum"}]}""");

        status.Should().Be(HttpStatusCode.OK);
        var groups = Data(body).ToDictionary(g => g.GetProperty("key").ToString(), g => g);
        groups.Should().HaveCount(2);
        groups.Values.Sum(g => g.GetProperty("count").GetInt32()).Should().Be(25);
        groups.Values.Select(g => g.GetProperty("summary")[0].GetDecimal()).Should().BeEquivalentTo([750m, 2500m]);
        groups.Values.Should().AllSatisfy(g => g.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Null));
    }

    [Fact]
    public async Task Should_ReturnNamesInsteadOfIds_WhenLoadHasSelect()
    {
        var (status, body) = await PostAsync("api/Orders/load",
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

    [Fact]
    public async Task Should_Answer400_WhenValuesNamesAReservedField()
    {
        var (status, body) = await SendAsync(HttpMethod.Get, "api/Orders/values/InternalNote");

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("detail").GetString().Should().NotContain("internal\"");
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
    public async Task Should_KeepTheReservedFields_WhenPut()
    {
        var (status, _) = await SendAsync(HttpMethod.Put, "api/Orders/1",
            """{"Id":1,"CustomerId":2,"Date":"2026-01-02","Amount":15,"Status":"Open"}""");

        status.Should().Be(HttpStatusCode.OK);
        await using var check = App.NewContext();
        var saved = await check.Orders.SingleAsync(o => o.Id == 1, Ct);
        saved.Amount.Should().Be(15);
        saved.InternalNote.Should().Be("internal");
    }

    [Theory]
    [InlineData("api/Orders/999", """{"Id":999,"CustomerId":1,"Date":"2026-01-02","Amount":1,"Status":"Open"}""")]
    [InlineData("api/Customers/999", """{"Id":999,"Name":"x","City":"y","Version":0}""")]
    public async Task Should_Answer404_WhenPutAMissingId(string url, string json)
    {
        var (status, _) = await SendAsync(HttpMethod.Put, url, json);

        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_Answer400_WhenCreateCarriesAnId()
    {
        var (status, body) = await SendAsync(HttpMethod.Post, "api/Customers", """{"Id":1,"Name":"Dup","City":"x"}""");

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("Code").GetString().Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task Should_StartAtVersionZero_WhenCreateCarriesAVersion()
    {
        var (status, body) = await SendAsync(HttpMethod.Post, "api/Customers", """{"Name":"V","City":"x","Version":99}""");

        status.Should().Be(HttpStatusCode.Created);
        body.GetProperty("Version").GetInt32().Should().Be(0);
        await using var check = App.NewContext();
        (await check.Customers.SingleAsync(c => c.Name == "V", Ct)).Version.Should().Be(0);
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

        (await SendAsync(HttpMethod.Delete, $"{location}?version=0")).Status.Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Get, location)).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_Answer409_WhenDeleteCarriesAStaleVersion()
    {
        await SendAsync(HttpMethod.Put, "api/Customers/2", """{"Id":2,"Name":"Bolt Two","City":"Milan","Version":0}""");   // now at 1

        var (status, body) = await SendAsync(HttpMethod.Delete, "api/Customers/2?version=0");

        status.Should().Be(HttpStatusCode.Conflict);
        body.GetProperty("Code").GetString().Should().Be("Conflict");
        await using var check = App.NewContext();
        (await check.Customers.AnyAsync(c => c.Id == 2, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_Answer400_WhenDeletingALockedRowWithoutItsVersion()
    {
        var (status, body) = await SendAsync(HttpMethod.Delete, "api/Customers/2");

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("detail").GetString().Should().Contain("version=");
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
