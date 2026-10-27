using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zerox.Ef.Domain;

namespace Zerox.Ef.Api.Tests;

public sealed class ErrorHandlingTests : IAsyncLifetime
{
    private TestHost? _host;

    private TestHost Host => _host ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host = await TestHost.StartAsync(app =>
        {
            // A real stale save: another context changes the row between the read and the save.
            app.MapPost("/conflict", async (HostDbContext db) =>
            {
                var doc = await db.Docs.SingleAsync();
                await using (var other = _host!.NewContext())
                {
                    (await other.Docs.SingleAsync()).Name = "theirs";
                    await other.SaveChangesAsync();
                }
                doc.Name = "mine";
                await db.SaveChangesAsync();
                return Results.Ok();
            });
            // An update of a row that does not exist: EF throws without a token involved.
            app.MapPost("/missing", async (HostDbContext db) =>
            {
                db.Plains.Update(new Plain { Id = 404, Name = "x" });
                await db.SaveChangesAsync();
                return Results.Ok();
            });
            app.MapGet("/invalid", IResult () => throw new InvalidRequestException("The field 'Nope' does not exist."));
            app.MapGet("/boom", IResult () => throw new InvalidOperationException("secret internals"));
            app.MapGet("/ok", () => Results.Ok(new { Answer = 42 }));
        });

        await using var seed = Host.NewContext();
        seed.Docs.Add(new Doc { Name = "first" });
        await seed.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Should_Answer409_WhenALockedRowWasChanged()
    {
        var response = await Host.Client.PostAsync("/conflict", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await BodyAsync(response);
        body.GetProperty("Code").GetString().Should().Be("Conflict");
        body.GetProperty("Message").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_LogAWarning_WhenAConflictHappens()
    {
        await Host.Client.PostAsync("/conflict", null, Ct);

        var middleware = Host.Logs.Entries.Where(e => e.Category.Contains("ErrorHandlingMiddleware", StringComparison.Ordinal)).ToList();
        middleware.Should().Contain(e => e.Level == LogLevel.Warning);
        middleware.Should().NotContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Should_Answer500_WhenAConcurrencyExceptionHasNoToken()
    {
        var response = await Host.Client.PostAsync("/missing", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await BodyAsync(response)).GetProperty("Code").GetString().Should().Be("InternalError");
    }

    [Fact]
    public async Task Should_Answer400WithTheReason_WhenAnInvalidRequestIsThrown()
    {
        var response = await Host.Client.GetAsync("/invalid", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await BodyAsync(response);
        body.GetProperty("Code").GetString().Should().Be("InvalidRequest");
        body.GetProperty("Message").GetString().Should().Be("The field 'Nope' does not exist.");
        Host.Logs.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task Should_Answer500WithoutDetails_WhenAnythingElseIsThrown()
    {
        var response = await Host.Client.GetAsync("/boom", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await response.Content.ReadAsStringAsync(Ct)).Should().NotContain("secret internals");
        Host.Logs.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Category.Contains("ErrorHandlingMiddleware", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_NotTouchTheResponse_WhenTheRequestSucceeds()
    {
        var response = await Host.Client.GetAsync("/ok", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BodyAsync(response)).GetProperty("Answer").GetInt32().Should().Be(42);
    }
}
