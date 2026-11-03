using Microsoft.EntityFrameworkCore;
using Zerox.Ef.Tests;

namespace Zerox.Ef.Domain.Tests;

// EF fails every UPDATE or DELETE that hits no row the same way; the conflict belongs only to the opt-in lock,
// recognised by a concurrency token on the failed entry.
public sealed class OptimisticLockConflictTests : IAsyncLifetime
{
    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await TestDb.CreateAsync();

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    [Fact]
    public async Task Should_RecogniseTheLock_WhenTheFailedEntryHasAToken()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, "SQLite has no row version: the lock is proved on PostgreSQL.");
        await using (var seed = Db.NewContext())
        {
            seed.Memos.Add(new Memo { Owner = "a", Text = "first" });
            await seed.SaveChangesAsync(Ct);
        }
        await using var mine = Db.NewContext();
        var memo = await mine.Memos.SingleAsync(Ct);
        await using (var other = Db.NewContext())
        {
            (await other.Memos.SingleAsync(Ct)).Text = "theirs";
            await other.SaveChangesAsync(Ct);
        }
        memo.Text = "mine";

        var conflict = await FluentActions.Awaiting(() => mine.SaveChangesAsync(Ct)).Should().ThrowAsync<DbUpdateConcurrencyException>();

        OptimisticLockConflict.Is(conflict.Which).Should().BeTrue();
    }

    [Fact]
    public async Task Should_NotRecogniseTheLock_WhenNoEntryHasAToken()
    {
        await using var context = Db.NewContext();
        context.Items.Update(new Item { Id = 404, Name = "x" });

        var missing = await FluentActions.Awaiting(() => context.SaveChangesAsync(Ct)).Should().ThrowAsync<DbUpdateConcurrencyException>();

        OptimisticLockConflict.Is(missing.Which).Should().BeFalse();
    }
}
