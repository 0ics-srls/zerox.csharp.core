using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zerox.Ef.Domain;
using Zerox.Ef.Tests;

namespace Zerox.Ef.Api.Tests;

public sealed class ErrorHandlingTests : IAsyncLifetime
{
    private TestHost? _host;

    private static readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            // An action that read the row and found another version than the client's.
            app.MapPost("/stale", IResult () => throw new StaleVersionException("Doc 1 changed since it was read."));
            app.MapGet("/boom", IResult () => throw new InvalidOperationException("secret internals"));
            app.MapGet("/ok", () => Results.Ok(new { Answer = 42 }));
            // An await using forgotten: what it saved was swallowed, and the answer must not say it was done.
            app.MapPost("/left-open", (Zerox.Ef.Core.IRepository<Doc> docs) =>
            {
                _ = docs.OpenUnitOfWork();
                return Results.NoContent();
            });
            // Fails after the first bytes went out: no status or body can be written any more.
            app.MapGet("/started", async (HttpContext context) =>
            {
                await context.Response.WriteAsync("partial");
                await context.Response.Body.FlushAsync();
                throw new InvalidOperationException("after the start");
            });
            // Waits until the client goes away.
            app.MapGet("/slow", async (HttpContext context) =>
            {
                Started.TrySetResult();
                await Task.Delay(Timeout.Infinite, context.RequestAborted);
            });
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
    public async Task Should_Answer500_WhenAnActionLeavesAUnitOfWorkOpen()
    {
        var response = await Host.Client.PostAsync("/left-open", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        Host.Logs.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Category.Contains("ErrorHandlingMiddleware", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_Answer409_WhenALockedRowWasChanged()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, "SQLite has no row version: the lock is proved on PostgreSQL.");
        var response = await Host.Client.PostAsync("/conflict", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await BodyAsync(response);
        body.GetProperty("Code").GetString().Should().Be("Conflict");
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_Answer409_WhenAStaleVersionIsThrown()
    {
        var response = await Host.Client.PostAsync("/stale", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await BodyAsync(response);
        body.GetProperty("Code").GetString().Should().Be("Conflict");
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_LogAWarning_WhenAConflictHappens()
    {
        await Host.Client.PostAsync("/stale", null, Ct);

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
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("Code").GetString().Should().Be("InvalidRequest");
        body.GetProperty("detail").GetString().Should().Be("The field 'Nope' does not exist.");
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

    // The middleware entries once the request has gone through it.
    private async Task<List<(LogLevel Level, string Category, string Message)>> MiddlewareLogsAsync(Func<(LogLevel Level, string Category, string Message), bool> until)
    {
        for (var i = 0; i < 500 && !Host.Logs.Entries.Any(until); i++)
            await Task.Delay(10, Ct);
        return Host.Logs.Entries.Where(e => e.Category.Contains("ErrorHandlingMiddleware", StringComparison.Ordinal)).ToList();
    }

    [Fact]
    public async Task Should_LogAndAbortWithoutASecondException_WhenTheResponseHasStarted()
    {
        Exception? cut = null;
        try
        {
            using var response = await Host.Client.GetAsync("/started", Ct);
            await response.Content.ReadAsStringAsync(Ct);
        }
        catch (HttpRequestException e)
        {
            cut = e;   // the connection is aborted: the client sees a cut response
        }

        cut.Should().NotBeNull();
        cut!.ToString().Should().NotContain("ErrorHandlingMiddleware");   // nothing escaped the middleware
        var logs = await MiddlewareLogsAsync(e => e.Level == LogLevel.Error);
        logs.Should().ContainSingle(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Should_NotLogAnError_WhenTheClientGoesAway()
    {
        using var gone = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var call = Host.Client.GetAsync("/slow", gone.Token);
        await Started.Task.WaitAsync(Ct);
        await gone.CancelAsync();
        await call.Invoking(c => c).Should().ThrowAsync<OperationCanceledException>();

        var logs = await MiddlewareLogsAsync(e => e.Category.Contains("ErrorHandlingMiddleware", StringComparison.Ordinal));
        logs.Should().ContainSingle(e => e.Level == LogLevel.Information);
        logs.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Should_NotTouchTheResponse_WhenTheRequestSucceeds()
    {
        var response = await Host.Client.GetAsync("/ok", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BodyAsync(response)).GetProperty("Answer").GetInt32().Should().Be(42);
    }
}
