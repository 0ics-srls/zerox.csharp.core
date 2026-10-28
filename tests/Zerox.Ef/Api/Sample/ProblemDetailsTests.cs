using System.Text;
using System.Text.Json;
using Zerox.Ef.Api.Tests.Sample.Api;

namespace Zerox.Ef.Api.Tests.Sample;

// One error format, the platform's: every failure — the ones the framework produces too — is a
// ProblemDetails with its status and the code of the domain, and the document says so.
public sealed class ProblemDetailsTests : IAsyncLifetime
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

    public static TheoryData<string, string, string, int, string> Failures => new()
    {
        { "POST", "api/Customers", "{", 400, "InvalidRequest" },                          // unreadable body
        { "POST", "api/Customers", """{"Name":1}""", 400, "InvalidRequest" },             // a value of the wrong type
        { "PUT", "api/Customers/1/name", """{"Version":0}""", 400, "InvalidRequest" },    // a required member missing
        { "POST", "api/Orders/load", """{"Take":"x"}""", 400, "InvalidRequest" },         // load options of the wrong type
        { "POST", "api/Orders/load", """{"Filter":["Nope","=",1]}""", 400, "InvalidRequest" }, // refused by the search
        { "POST", "api/Customers", """{"Id":5,"Name":"a"}""", 400, "InvalidRequest" },    // refused by the controller
        { "GET", "api/Customers/999", "", 404, "NotFound" },                              // no row with that id
        { "GET", "api/Customers/abc", "", 404, "NotFound" },                              // no route
        { "DELETE", "api/Customers/1", "", 400, "InvalidRequest" },                       // a locked row without its version
        { "DELETE", "api/Customers/1?version=7", "", 409, "Conflict" },                   // a stale version
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Should_AnswerAProblemDetailsWithTheCode_ForEveryFailure(string method, string url, string json, int status, string code)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = json.Length == 0 ? null : new StringContent(json, Encoding.UTF8, "application/json"),
        };
        using var response = await App.Client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);

        ((int)response.StatusCode).Should().Be(status, text);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json", text);
        using var body = JsonDocument.Parse(text);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(status);
        body.RootElement.GetProperty("Code").GetString().Should().Be(code);
    }

    [Fact]
    public async Task Should_NameTheFields_WhenTheBodyFailsItsBinding()
    {
        using var content = new StringContent("""{"Version":0}""", Encoding.UTF8, "application/json");
        using var response = await App.Client.PutAsync(new Uri("api/Customers/1/name", UriKind.Relative), content, Ct);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("errors").TryGetProperty("Name", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Should_DeclareTheProblemDetails_ForEveryErrorTheDocumentLists()
    {
        using var response = await App.Client.GetAsync(new Uri("openapi/v1.json", UriKind.Relative), Ct);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var root = document.RootElement;

        var errors = root.GetProperty("paths").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateObject())
            .SelectMany(o => o.Value.GetProperty("responses").EnumerateObject())
            .Where(r => int.Parse(r.Name, System.Globalization.CultureInfo.InvariantCulture) >= 400)
            .ToList();
        errors.Should().NotBeEmpty();
        errors.Should().AllSatisfy(r => r.Value.GetProperty("content").GetProperty("application/problem+json")
            .GetProperty("schema").GetProperty("$ref").GetString().Should().Be("#/components/schemas/ProblemDetails"));
        var problem = root.GetProperty("components").GetProperty("schemas").GetProperty("ProblemDetails").GetProperty("properties");
        problem.TryGetProperty("Code", out _).Should().BeTrue();
        problem.TryGetProperty("errors", out _).Should().BeTrue();
    }
}
