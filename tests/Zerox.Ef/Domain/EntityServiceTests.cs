using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;
using Zerox.Ef.Tests;

namespace Zerox.Ef.Domain.Tests;

// The operations of an entity as results: every outcome a status, never an exception. A conflict needs a row
// version, so those tests run on PostgreSQL.
public sealed class EntityServiceTests : IAsyncLifetime
{
    private const string PostgresOnly = "SQLite has no row version: the lock is proved on PostgreSQL.";

    private TestDb? _database;
    private int _boxId;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 30 items in one box: Name item01..item30, Quantity 1..30, Secret "seeded".
    public async ValueTask InitializeAsync()
    {
        _database = await TestDb.CreateAsync();
        await using var seed = Db.NewContext();
        var box = new Box { Label = "box", Opened = new DateOnly(2026, 1, 1) };
        seed.Boxes.Add(box);
        for (var q = 1; q <= 30; q++)
            seed.Items.Add(new Item { Name = $"item{q:00}", Quantity = q, Box = box, Secret = "seeded" });
        await seed.SaveChangesAsync(Ct);
        _boxId = box.Id;
    }

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    // A derived service: its page limit, and RunAsync open to the tests.
    private sealed class Probe(IRepository<Item> repository, int maxTake = Search.DefaultMaxTake) : EntityService<Item>(repository)
    {
        protected override int MaxTake => maxTake;

        public IRepository<Item> Items => Repository;

        public Task<OpResult<TResult>> Run<TResult>(Func<CancellationToken, Task<OpResult<TResult>>> operation) =>
            RunAsync(operation, Ct);
    }

    // A derived service with its own view of the rows.
    private sealed class SmallOnes(IRepository<Item> repository) : EntityService<Item>(repository)
    {
        protected override IQueryable<Item> SearchSource() => Repository.Query().Where(i => i.Quantity <= 3);
    }

    private static IRepository<T> RepositoryOf<T>(AsyncServiceScope scope) where T : class, IEntity =>
        scope.ServiceProvider.GetRequiredService<IRepository<T>>();

    private static EntityService<T> ServiceOf<T>(AsyncServiceScope scope) where T : class, IEntity => new(RepositoryOf<T>(scope));

    private async Task<Memo> SeedMemoAsync()
    {
        await using var seed = Db.NewContext();
        var memo = new Memo { Owner = "a", Text = "first", Note = "n0" };
        seed.Memos.Add(memo);
        await seed.SaveChangesAsync(Ct);
        return memo;
    }

    // The row as the database has it now: the version a client read comes from here, never a literal.
    private async Task<Memo?> StoredMemoAsync(int id)
    {
        await using var check = Db.NewContext();
        return await check.Memos.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, Ct);
    }

    private async Task<Item?> StoredItemAsync(int id)
    {
        await using var check = Db.NewContext();
        return await check.Items.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, Ct);
    }

    private async Task<int> ItemIdAsync(int quantity)
    {
        await using var check = Db.NewContext();
        return await check.Items.Where(i => i.Quantity == quantity).Select(i => i.Id).SingleAsync(Ct);
    }

    // Another writer saves the same memo, in its own context.
    private async Task OtherWriterAsync(int id, string note)
    {
        await using var other = Db.NewContext();
        (await other.Memos.SingleAsync(m => m.Id == id, Ct)).Note = note;
        await other.SaveChangesAsync(Ct);
    }

    // --- load ---

    [Fact]
    public async Task Should_LoadThePage_WhenTheOptionsAreValid()
    {
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Item>(scope).LoadAsync(
            new LoadOptions { Take = 5, Sort = [new() { Selector = "Quantity", Desc = true }], RequireTotalCount = true }, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value!.Data.Cast<Item>().Select(i => i.Quantity).Should().Equal(30, 29, 28, 27, 26);
        result.Value.TotalCount.Should().Be(30);
    }

    [Fact]
    public async Task Should_ReturnInvalidWithTheReason_WhenRowsAreAskedWithoutTake()
    {
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Item>(scope).LoadAsync(new LoadOptions(), Ct);

        result.Status.Should().Be(OpStatus.Invalid);
        result.Reason.Should().Contain("Take");
    }

    [Fact]
    public async Task Should_ReturnInvalid_WhenTakeIsAboveMaxTake()
    {
        await using var scope = Db.NewScope();
        var service = new Probe(RepositoryOf<Item>(scope), maxTake: 10);

        var result = await service.LoadAsync(new LoadOptions { Take = 11 }, Ct);

        result.Status.Should().Be(OpStatus.Invalid);
        result.Reason.Should().Contain("10");
    }

    [Fact]
    public async Task Should_LoadOnlyTheSearchSource_WhenItIsOverridden()
    {
        await using var scope = Db.NewScope();
        var service = new SmallOnes(RepositoryOf<Item>(scope));

        var result = await service.LoadAsync(new LoadOptions { Take = 50, RequireTotalCount = true }, Ct);

        result.Value!.TotalCount.Should().Be(3);
    }

    // --- values ---

    [Fact]
    public async Task Should_ReturnTheDistinctValues_WhenTheColumnIsText()
    {
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Item>(scope).ValuesAsync("Name", new LoadOptions(), Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value.Should().Equal(Enumerable.Range(1, 30).Select(q => new TextValue($"item{q:00}", 1)));
    }

    [Fact]
    public async Task Should_ReturnOnlyTheValuesOfTheFilteredRows_WhenTheOptionsHaveAFilter()
    {
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Item>(scope).ValuesAsync("Name", new LoadOptions { Filter = ["Quantity", "<=", 2] }, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value.Should().Equal(new TextValue("item01", 1), new TextValue("item02", 1));
    }

    [Fact]
    public async Task Should_ReturnOnlyTheValuesOfTheSearchSource_WhenItIsOverridden()
    {
        await using var scope = Db.NewScope();
        var service = new SmallOnes(RepositoryOf<Item>(scope));

        var result = await service.ValuesAsync("Name", new LoadOptions(), Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value.Should().Equal(new TextValue("item01", 1), new TextValue("item02", 1), new TextValue("item03", 1));
    }

    [Fact]
    public async Task Should_ApplyTheFilterInsideTheSearchSource_WhenBothNarrow()
    {
        await using var scope = Db.NewScope();
        var service = new SmallOnes(RepositoryOf<Item>(scope));

        var result = await service.ValuesAsync("Name", new LoadOptions { Filter = ["Quantity", ">=", 3] }, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value.Should().Equal(new TextValue("item03", 1));
    }

    [Fact]
    public async Task Should_ReturnTheValuesOfTheRowsTheLoadReturns_WhenTheSameFilterIsGiven()
    {
        await using var scope = Db.NewScope();
        IReadOnlyList<object?> filter = ["Quantity", "<=", 5];

        var loaded = await ServiceOf<Item>(scope).LoadAsync(new LoadOptions { Filter = filter, Take = 50, RequireTotalCount = true }, Ct);
        var values = await ServiceOf<Item>(scope).ValuesAsync("Name", new LoadOptions { Filter = filter }, Ct);

        loaded.Status.Should().Be(OpStatus.Ok);
        values.Value!.Select(v => v.Value)
            .Should().Equal(loaded.Value!.Data.Cast<Item>().Select(i => i.Name).Distinct().OrderBy(n => n));
        values.Value!.Sum(v => v.Count).Should().Be(loaded.Value.TotalCount);
    }

    [Fact]
    public async Task Should_IgnoreThePageAndTheSortOfTheOptions_WhenValuesAreAsked()
    {
        await using var scope = Db.NewScope();
        var options = new LoadOptions { Take = 1, Skip = 5, Sort = [new SortingInfo { Selector = "Quantity", Desc = true }] };

        var result = await ServiceOf<Item>(scope).ValuesAsync("Name", options, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value.Should().Equal(Enumerable.Range(1, 30).Select(q => new TextValue($"item{q:00}", 1)));
    }

    [Fact]
    public async Task Should_ReturnAtMostMaxTakeValues_WhenThereAreMore()
    {
        await using var scope = Db.NewScope();
        var service = new Probe(RepositoryOf<Item>(scope), maxTake: 10);

        var result = await service.ValuesAsync("Name", new LoadOptions(), Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value.Should().Equal(Enumerable.Range(1, 10).Select(q => new TextValue($"item{q:00}", 1)));
    }

    [Theory]
    [InlineData("Quantity")]
    [InlineData("Secret")]
    [InlineData("Nope")]
    public async Task Should_ReturnInvalid_WhenTheColumnIsNotReadableText(string column)
    {
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Item>(scope).ValuesAsync(column, new LoadOptions(), Ct);

        result.Status.Should().Be(OpStatus.Invalid);
        result.Reason.Should().Contain(column);
    }

    [Fact]
    public async Task Should_ReturnInvalid_WhenTheFilterNamesAReservedField()
    {
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Item>(scope).ValuesAsync("Name", new LoadOptions { Filter = ["Secret", "=", "s"] }, Ct);

        result.Status.Should().Be(OpStatus.Invalid);
        result.Reason.Should().Contain("Secret");
    }

    // --- get ---

    [Fact]
    public async Task Should_ReturnTheRow_WhenGotById()
    {
        var id = await ItemIdAsync(7);
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Item>(scope).GetAsync(id, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value!.Quantity.Should().Be(7);
    }

    [Fact]
    public async Task Should_ReturnNotFound_WhenTheIdIsMissing()
    {
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Item>(scope).GetAsync(404, Ct);

        result.Status.Should().Be(OpStatus.NotFound);
    }

    // --- create ---

    [Fact]
    public async Task Should_CreateTheRow_WhenItHasNoId()
    {
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Item>(scope).CreateAsync(new Item { Name = "new", BoxId = _boxId }, Ct);

        result.Status.Should().Be(OpStatus.Created);
        result.Value!.Id.Should().BePositive();
        (await StoredItemAsync(result.Value.Id))!.Name.Should().Be("new");
    }

    [Fact]
    public async Task Should_ReturnInvalid_WhenTheCreatedRowCarriesAnId()
    {
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Item>(scope).CreateAsync(new Item { Id = 7000, Name = "new", BoxId = _boxId }, Ct);

        result.Status.Should().Be(OpStatus.Invalid);
        (await StoredItemAsync(7000)).Should().BeNull();
    }

    // --- update ---

    [Fact]
    public async Task Should_SaveTheRowAndAnswerItAsStored_WhenUpdated()
    {
        var read = await SeedMemoAsync();
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Memo>(scope).UpdateAsync(
            new Memo { Id = read.Id, Owner = read.Owner, Text = "mine", Note = read.Note, Version = read.Version }, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        var stored = await StoredMemoAsync(read.Id);
        stored!.Text.Should().Be("mine");
        (result.Value!.Text, result.Value.Version).Should().Be((stored.Text, stored.Version));
    }

    [Fact]
    public async Task Should_KeepTheReservedFields_WhenUpdated()
    {
        var sent = (await StoredItemAsync(await ItemIdAsync(5)))!;
        sent.Name = "renamed";
        sent.Secret = "changed";
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Item>(scope).UpdateAsync(sent, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        var stored = await StoredItemAsync(sent.Id);
        (stored!.Name, stored.Secret).Should().Be(("renamed", "seeded"));
    }

    [Fact]
    public async Task Should_ReturnNotFound_WhenUpdatingAMissingRow()
    {
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Memo>(scope).UpdateAsync(new Memo { Id = 404, Owner = "a", Text = "x", Note = "n" }, Ct);

        result.Status.Should().Be(OpStatus.NotFound);
    }

    [Fact]
    public async Task Should_ReturnConflict_WhenUpdatingAtAStaleVersion()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var read = await SeedMemoAsync();
        await OtherWriterAsync(read.Id, "theirs");
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Memo>(scope).UpdateAsync(
            new Memo { Id = read.Id, Owner = read.Owner, Text = "mine", Note = read.Note, Version = read.Version }, Ct);

        result.Status.Should().Be(OpStatus.Conflict);
        result.Reason.Should().Be("The row was changed by another user.");
        var stored = await StoredMemoAsync(read.Id);
        (stored!.Text, stored.Note).Should().Be(("first", "theirs"));
    }

    // --- delete ---

    [Fact]
    public async Task Should_DeleteAtTheVersionRead_WhenDeleted()
    {
        var read = await SeedMemoAsync();
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Memo>(scope).DeleteAsync(read.Id, (await StoredMemoAsync(read.Id))!.Version, Ct);

        result.Status.Should().Be(OpStatus.Deleted);
        (await StoredMemoAsync(read.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnInvalid_WhenALockedRowIsDeletedWithoutAVersion()
    {
        var memo = await SeedMemoAsync();
        var itemId = await ItemIdAsync(9);
        await using var scope = Db.NewScope();

        var outcomes = (Memo: await ServiceOf<Memo>(scope).DeleteAsync(memo.Id, null, Ct), Item: await ServiceOf<Item>(scope).DeleteAsync(itemId, null, Ct));

        outcomes.Memo.Status.Should().Be(OpStatus.Invalid);
        outcomes.Memo.Reason.Should().Contain("?version=N");
        outcomes.Item.Status.Should().Be(OpStatus.Deleted);
    }

    [Fact]
    public async Task Should_ReturnInvalid_WhenTheVersionDoesNotFitTheRowVersion()
    {
        var memo = await SeedMemoAsync();
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Memo>(scope).DeleteAsync(memo.Id, (ulong)uint.MaxValue + 1, Ct);

        result.Status.Should().Be(OpStatus.Invalid);
        (await StoredMemoAsync(memo.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Should_ReturnNotFound_WhenDeletingAMissingRow()
    {
        var another = await SeedMemoAsync();
        var versionOfAnother = (await StoredMemoAsync(another.Id))!.Version;
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Memo>(scope).DeleteAsync(404, versionOfAnother, Ct);

        result.Status.Should().Be(OpStatus.NotFound);
    }

    [Fact]
    public async Task Should_ReturnConflict_WhenDeletingAtAStaleVersion()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var read = await SeedMemoAsync();
        await OtherWriterAsync(read.Id, "theirs");
        await using var scope = Db.NewScope();

        var result = await ServiceOf<Memo>(scope).DeleteAsync(read.Id, read.Version, Ct);

        result.Status.Should().Be(OpStatus.Conflict);
        (await StoredMemoAsync(read.Id)).Should().NotBeNull();
    }

    // --- RunAsync: the one place where an exception becomes a status ---

    [Fact]
    public async Task Should_ReturnInvalid_WhenTheOperationThrowsInvalidRequest()
    {
        await using var scope = Db.NewScope();
        var probe = new Probe(RepositoryOf<Item>(scope));

        var result = await probe.Run<int>(_ => throw new InvalidRequestException("bad"));

        (result.Status, result.Reason).Should().Be((OpStatus.Invalid, "bad"));
    }

    [Fact]
    public async Task Should_ReturnConflict_WhenTheOperationThrowsStaleVersion()
    {
        await using var scope = Db.NewScope();
        var probe = new Probe(RepositoryOf<Item>(scope));

        var result = await probe.Run<int>(_ => throw new StaleVersionException("stale"));

        (result.Status, result.Reason).Should().Be((OpStatus.Conflict, "stale"));
    }

    [Fact]
    public async Task Should_ReturnFaultedWithTheException_WhenTheOperationThrowsAnythingElse()
    {
        await using var scope = Db.NewScope();
        var probe = new Probe(RepositoryOf<Item>(scope));
        var boom = new InvalidOperationException("boom");

        var result = await probe.Run<int>(_ => throw boom);

        result.Status.Should().Be(OpStatus.Faulted);
        result.Exception.Should().BeSameAs(boom);
    }

    [Fact]
    public async Task Should_ReturnFaulted_WhenTheOperationIsCancelled()
    {
        await using var scope = Db.NewScope();
        var probe = new Probe(RepositoryOf<Item>(scope));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var result = await probe.Run(async _ =>
        {
            await Task.Delay(Timeout.Infinite, cancelled.Token);
            return OpResult.Ok(0);
        });

        result.Status.Should().Be(OpStatus.Faulted);
        result.Exception.Should().BeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ReturnFaulted_WhenAConcurrencyExceptionHasNoToken()
    {
        var id = await ItemIdAsync(3);
        await using var scope = Db.NewScope();
        var probe = new Probe(RepositoryOf<Item>(scope));

        var result = await probe.Run(async c =>
        {
            var item = (await probe.Items.GetByIdAsync(id, c))!;
            await using (var other = Db.NewContext())
            {
                other.Items.Remove(await other.Items.SingleAsync(i => i.Id == id, c));
                await other.SaveChangesAsync(c);
            }
            item.Name = "mine";
            return OpResult.Ok(await probe.Items.SaveChangesAsync(c));
        });

        result.Status.Should().Be(OpStatus.Faulted);
        result.Exception.Should().BeOfType<DbUpdateConcurrencyException>();
    }

    // --- misuse ---

    [Fact]
    public async Task Should_ThrowArgumentNull_WhenTheEntityOrOptionsAreNull()
    {
        await using var scope = Db.NewScope();
        var service = ServiceOf<Item>(scope);

        Func<Task>[] calls =
        [
            () => service.CreateAsync(null!, Ct),
            () => service.UpdateAsync(null!, Ct),
            () => service.LoadAsync(null!, Ct),
        ];

        foreach (var call in calls)
            await call.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Should_ThrowArgumentNull_WhenTheColumnOrTheOptionsAreNull()
    {
        await using var scope = Db.NewScope();
        var service = ServiceOf<Item>(scope);

        Func<Task>[] calls =
        [
            () => service.ValuesAsync(null!, new LoadOptions(), Ct),
            () => service.ValuesAsync("Name", null!, Ct),
        ];

        foreach (var call in calls)
            await call.Should().ThrowAsync<ArgumentNullException>();
    }
}
