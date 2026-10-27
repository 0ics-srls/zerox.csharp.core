using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Core.Data;

namespace Zerox.Domain.Tests;

public sealed class LastWriterWinsTests : IAsyncLifetime
{
    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await TestDb.CreateAsync();

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    private async Task<int> SeedAsync(string owner = "a")
    {
        await using var seed = Db.NewContext();
        var memo = new Memo { Owner = owner, Text = "first", Note = "n0" };
        seed.Memos.Add(memo);
        await seed.SaveChangesAsync(Ct);
        return memo.Id;
    }

    private async Task<Memo> ReadAsync(int id)
    {
        await using var check = Db.NewContext();
        return await check.Memos.SingleAsync(m => m.Id == id, Ct);
    }

    // Another writer saves the same row: its own context, its own change.
    private async Task OtherWriterAsync(int id, Action<Memo> change)
    {
        await using var other = Db.NewContext();
        var memo = await other.Memos.SingleAsync(m => m.Id == id, Ct);
        change(memo);
        await other.SaveChangesAsync(Ct);
    }

    private async Task OtherWriterDeletesAsync(int id)
    {
        await using var other = Db.NewContext();
        other.Memos.Remove(await other.Memos.SingleAsync(m => m.Id == id, Ct));
        await other.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Should_RunOnce_WhenThereIsNoConflict()
    {
        var id = await SeedAsync();
        await using var scope = Db.NewScope();
        var wins = new LastWriterWins(scope.ServiceProvider.GetRequiredService<IRepositoryFactory>());
        var calls = 0;

        var saved = await wins.SaveAsync<Memo>(id, _ => true, (m, _) => { calls++; m.Text = "mine"; return Task.CompletedTask; }, ct: Ct);

        calls.Should().Be(1);
        saved!.Text.Should().Be("mine");
        (await ReadAsync(id)).Version.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReReadAndSave_WhenAConflictHappens()
    {
        var id = await SeedAsync();
        await using var scope = Db.NewScope();
        var wins = new LastWriterWins(scope.ServiceProvider.GetRequiredService<IRepositoryFactory>());
        var calls = 0;

        var saved = await wins.SaveAsync<Memo>(id, _ => true, async (m, ct) =>
        {
            if (++calls == 1)
                await OtherWriterAsync(id, other => other.Note = "theirs");
            m.Text = "mine";
        }, ct: Ct);

        calls.Should().Be(2);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_KeepTheLastWritersValues_AfterTheRetry()
    {
        var id = await SeedAsync();
        await using var scope = Db.NewScope();
        var wins = new LastWriterWins(scope.ServiceProvider.GetRequiredService<IRepositoryFactory>());
        var calls = 0;

        await wins.SaveAsync<Memo>(id, _ => true, async (m, ct) =>
        {
            if (++calls == 1)
                await OtherWriterAsync(id, other => { other.Text = "theirs"; other.Note = "their note"; });
            m.Text = "mine";
        }, ct: Ct);

        var row = await ReadAsync(id);
        row.Text.Should().Be("mine");
        row.Note.Should().Be("their note");
    }

    [Fact]
    public async Task Should_HaveTheVersionIncrementedByBothWriters_AfterTheRetry()
    {
        var id = await SeedAsync();
        await using var scope = Db.NewScope();
        var wins = new LastWriterWins(scope.ServiceProvider.GetRequiredService<IRepositoryFactory>());
        var calls = 0;

        await wins.SaveAsync<Memo>(id, _ => true, async (m, ct) =>
        {
            if (++calls == 1)
                await OtherWriterAsync(id, other => other.Note = "theirs");
            m.Text = "mine";
        }, ct: Ct);

        (await ReadAsync(id)).Version.Should().Be(2);
    }

    [Fact]
    public async Task Should_Throw_WhenAttemptsAreExhausted()
    {
        var id = await SeedAsync();
        await using var scope = Db.NewScope();
        var wins = new LastWriterWins(scope.ServiceProvider.GetRequiredService<IRepositoryFactory>());
        var calls = 0;

        var save = () => wins.SaveAsync<Memo>(id, _ => true, async (m, ct) =>
        {
            calls++;
            await OtherWriterAsync(id, other => other.Note = $"theirs {calls}");
            m.Text = "mine";
        }, ct: Ct);

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
        calls.Should().Be(LastWriterWins.MaxAttempts);
    }

    [Fact]
    public async Task Should_ReturnNullWithoutRetry_WhenTheRowDisappears()
    {
        var id = await SeedAsync();
        await using var scope = Db.NewScope();
        var wins = new LastWriterWins(scope.ServiceProvider.GetRequiredService<IRepositoryFactory>());
        var calls = 0;

        var saved = await wins.SaveAsync<Memo>(id, _ => true, async (m, ct) =>
        {
            calls++;
            await OtherWriterDeletesAsync(id);
            m.Text = "mine";
        }, ct: Ct);

        saved.Should().BeNull();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Should_DetectAStaleClient_WhenTheClientVersionIsGiven()
    {
        var id = await SeedAsync();
        await OtherWriterAsync(id, other => other.Note = "theirs");   // the row is at Version 1
        await using var scope = Db.NewScope();
        var wins = new LastWriterWins(scope.ServiceProvider.GetRequiredService<IRepositoryFactory>());
        var calls = 0;

        var saved = await wins.SaveAsync<Memo>(id, _ => true,
            (m, _) => { calls++; m.Text = "mine"; return Task.CompletedTask; }, versionReadByClient: 0, ct: Ct);

        calls.Should().Be(2);   // the first save saw the stale client, the retry re-read and won
        saved!.Text.Should().Be("mine");
        (await ReadAsync(id)).Version.Should().Be(2);
    }

    [Fact]
    public async Task Should_NotOpenATransaction()
    {
        var id = await SeedAsync();
        await using var scope = Db.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var wins = new LastWriterWins(scope.ServiceProvider.GetRequiredService<IRepositoryFactory>());
        var inside = new List<bool>();

        await wins.SaveAsync<Memo>(id, _ => true, (m, _) =>
        {
            inside.Add(context.Database.CurrentTransaction is null);
            m.Text = "mine";
            return Task.CompletedTask;
        }, ct: Ct);

        inside.Should().Equal(true);
    }

    [Fact]
    public async Task Should_NotTouchARowOutsideTheScope()
    {
        var id = await SeedAsync(owner: "b");
        await using var scope = Db.NewScope();
        var wins = new LastWriterWins(scope.ServiceProvider.GetRequiredService<IRepositoryFactory>());
        var calls = 0;

        var saved = await wins.SaveAsync<Memo>(id, m => m.Owner == "a",
            (m, _) => { calls++; m.Text = "mine"; return Task.CompletedTask; }, ct: Ct);

        saved.Should().BeNull();
        calls.Should().Be(0);
        (await ReadAsync(id)).Text.Should().Be("first");
    }
}
