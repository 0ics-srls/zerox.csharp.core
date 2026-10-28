using System.Net;
using System.Text.Json;
using Zerox.Ef.Api.Tests.Sample.Api;

namespace Zerox.Ef.Api.Tests.Sample;

// The contract of the example application: the OpenAPI document it serves, kept in the repository as
// openapi/Sample.json (the TypeScript client is generated from that file), and the Scalar UI on it.
public sealed class OpenApiDocumentTests : IAsyncLifetime
{
    private SampleApp? _app;

    private SampleApp App => _app ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _app = await SampleApp.StartAsync();

    public async ValueTask DisposeAsync() => await App.DisposeAsync();

    // The file in the source tree, found from the build output up to the solution.
    public static string DocumentFile
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "zerox.csharp.core.slnx")))
                    return Path.Combine(dir.FullName, "tests", "Zerox.Ef", "Api", "Sample", "openapi", "Sample.json");
            throw new InvalidOperationException("zerox.csharp.core.slnx not found above " + AppContext.BaseDirectory);
        }
    }

    private async Task<string> ServedAsync()
    {
        using var response = await App.Client.GetAsync(new Uri("openapi/v1.json", UriKind.Relative), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(Ct);
    }

    [Fact]
    public async Task Should_KeepTheFileEqualToTheServedDocument()
    {
        var served = (await ServedAsync()).ReplaceLineEndings("\n");
        var kept = File.Exists(DocumentFile) ? (await File.ReadAllTextAsync(DocumentFile, Ct)).ReplaceLineEndings("\n") : null;

        if (kept != served)
            await File.WriteAllTextAsync(DocumentFile, served, Ct);

        kept.Should().Be(served, "the contract changed: openapi/Sample.json has been rewritten, read the diff and run the tests again");
    }

    [Fact]
    public async Task Should_NameEveryOperation_ByControllerAndAction()
    {
        using var document = JsonDocument.Parse(await ServedAsync());

        var operations = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateObject())
            .Select(o => o.Value.TryGetProperty("operationId", out var id) ? id.GetString() : null)
            .ToList();
        operations.Should().NotBeEmpty().And.OnlyHaveUniqueItems().And.NotContainNulls();
        operations.Should().Contain(["Customers_Load", "Orders_Load", "Customers_GetById", "Customers_MergeInto"]);
    }

    [Fact]
    public async Task Should_DocumentTheLoadWithTheEntityItLists()
    {
        using var document = JsonDocument.Parse(await ServedAsync());
        var root = document.RootElement;

        var response = root.GetProperty("paths").GetProperty("/api/Orders/load").GetProperty("post")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        response.GetProperty("$ref").GetString().Should().Be("#/components/schemas/LoadResultOfOrder");
        var data = root.GetProperty("components").GetProperty("schemas").GetProperty("LoadResultOfOrder")
            .GetProperty("properties").GetProperty("data").GetProperty("oneOf").EnumerateArray()
            .Select(s => s.GetProperty("items").GetProperty("$ref").GetString()).ToList();
        data.Should().Equal("#/components/schemas/Order", "#/components/schemas/LoadGroup");
    }

    [Fact]
    public async Task Should_ServeTheScalarUiOnTheDocument()
    {
        using var response = await App.Client.GetAsync(new Uri("scalar/v1", UriKind.Relative), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("openapi/v1.json");
    }
}
