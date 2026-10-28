using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Zerox.Ef.Api.Tests;

public enum Shade
{
    Light,
    Dark,
}

public sealed record Dated(int Id, DateOnly CreatedOn, DateOnly? ClosedOn, Shade Shade);

public sealed class JsonTests : IAsyncLifetime
{
    private TestHost? _host;

    private TestHost Host => _host ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        _host = await TestHost.StartAsync(app =>
        {
            app.MapGet("/dated", () => Results.Ok(new Dated(1, new DateOnly(2026, 10, 27), null, Shade.Dark)));
            app.MapPost("/echo", (Dated dated) => Results.Ok(dated));
        });

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();

    [Fact]
    public async Task Should_KeepPropertyNamesAsIs_WhenSerializing()
    {
        var json = await Host.Client.GetStringAsync("/dated", Ct);

        json.Should().Contain("\"Id\":1").And.Contain("\"CreatedOn\":").And.NotContain("\"id\"").And.NotContain("\"createdOn\"");
    }

    [Fact]
    public async Task Should_WriteEnumsAsNames()
    {
        var json = await Host.Client.GetStringAsync("/dated", Ct);

        json.Should().Contain("\"Shade\":\"Dark\"");
    }

    [Fact]
    public async Task Should_WriteAndReadDateOnlyAsIsoDay()
    {
        (await Host.Client.GetStringAsync("/dated", Ct)).Should().Contain("\"CreatedOn\":\"2026-10-27\"").And.Contain("\"ClosedOn\":null");

        var sent = """{"Id":2,"CreatedOn":"2026-01-31","ClosedOn":"2026-02-01","Shade":"Light"}""";
        var response = await Host.Client.PostAsync("/echo", new StringContent(sent, System.Text.Encoding.UTF8, "application/json"), Ct);

        (await response.Content.ReadAsStringAsync(Ct)).Should().Be(sent);
    }

    [Fact]
    public async Task Should_RefuseANumberWrittenAsAString()
    {
        var sent = """{"Id":"2","CreatedOn":"2026-01-31","ClosedOn":null,"Shade":"Light"}""";

        var response = await Host.Client.PostAsync("/echo", new StringContent(sent, System.Text.Encoding.UTF8, "application/json"), Ct);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public void Should_ApplyTheSameOptions_ToHttpJsonOptions()
    {
        var options = Host.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
        var mvc = Host.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;

        foreach (var serializer in new[] { options, mvc })
        {
            serializer.PropertyNamingPolicy.Should().BeNull();
            serializer.Converters.Select(c => c.GetType().Name).Should()
                .Contain(["JsonStringEnumConverter", "DateOnlyJsonConverter", "NullableDateOnlyJsonConverter"]);
        }
    }
}
