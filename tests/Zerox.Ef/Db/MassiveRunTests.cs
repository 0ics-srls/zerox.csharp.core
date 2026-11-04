using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db.Tests;

// RunMassiveAsync: the exclusive mode inside the provider's execution strategy, so it works whether or not the provider
// retries (PostgreSQL's EnableRetryOnFailure refuses a transaction opened outside the strategy).
public sealed class MassiveRunTests : IAsyncLifetime
{
    private const string Seeded = "seeded";
    private const string Edited = "edited";

    private TestDb? _database;
    private ServiceProvider? _retrying;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDb.CreateAsync();
        _retrying = new ServiceCollection()
            .AddZeroxDb<TestDbContext>(o => Db.Use(o).ReplaceService<IExecutionStrategyFactory, RetryingStrategyFactory>())
            .BuildServiceProvider();
        await using var seed = Db.NewContext();
        seed.Items.Add(new Item { Name = Seeded, Quantity = 1 });
        await seed.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_retrying is not null)
            await _retrying.DisposeAsync();
        await Db.DisposeAsync();
    }

    private AsyncServiceScope RetryingScope() => (_retrying ?? throw new InvalidOperationException("InitializeAsync has not run.")).CreateAsyncScope();

    private async Task<List<string>> NamesAsync()
    {
        await using var check = Db.NewContext();
        return await check.Items.OrderBy(i => i.Name).Select(i => i.Name).ToListAsync(Ct);
    }

    private static Task<int> RenameAsync(IMassiveOpHandler<Item> massive, CancellationToken ct) =>
        massive.UpdateWhereAsync(i => i.Name == Seeded, fields => fields.Set(i => i.Name, Edited), ct);

    [Fact]
    public async Task Should_Commit_WhenTheWorkReturns()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();

        var updated = await items.RunMassiveAsync(RenameAsync, Ct);

        updated.Should().Be(1);
        (await NamesAsync()).Should().Equal(Edited);
    }

    [Fact]
    public async Task Should_RollBack_WhenTheWorkThrows()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();

        var run = () => items.RunMassiveAsync<int>(async (massive, ct) =>
        {
            await RenameAsync(massive, ct);
            throw new InvalidOperationException("The work changed its mind.");
        }, Ct);

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("The work changed its mind.");
        (await NamesAsync()).Should().Equal(Seeded);
    }

    [Fact]
    public async Task Should_ThrowOnSave_WhileTheWorkRuns()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();

        var run = () => items.RunMassiveAsync(async (_, ct) => await items.SaveChangesAsync(ct), Ct);

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("A massive operation is open*");
    }

    [Fact]
    public async Task Should_NotRun_WhenThereArePendingChanges()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        await items.AddAsync(new Item { Name = "pending" }, Ct);

        var run = () => items.RunMassiveAsync(RenameAsync, Ct);

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("There are pending changes*");
    }

    [Fact]
    public async Task Should_LeaveTheRepositorySaving_WhenTheWorkEnded()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        await items.RunMassiveAsync(RenameAsync, Ct);
        await items.AddAsync(new Item { Name = "after" }, Ct);

        await items.SaveChangesAsync(Ct);

        (await NamesAsync()).Should().Equal("after", Edited);
    }

    [Fact]
    public async Task Should_Commit_WhenTheProviderRetries()
    {
        await using var scope = RetryingScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();

        var updated = await items.RunMassiveAsync(RenameAsync, Ct);

        updated.Should().Be(1);
        (await NamesAsync()).Should().Equal(Edited);
    }

    [Fact]
    public async Task Should_RunTheWorkAgainFromTheStart_WhenAFailureIsTransient()
    {
        await using var scope = RetryingScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        var attempts = 0;

        var updated = await items.RunMassiveAsync(async (massive, ct) =>
        {
            attempts++;
            var renamed = await RenameAsync(massive, ct);
            return attempts == 1 ? throw new TransientFailureException() : renamed;
        }, Ct);

        (attempts, updated).Should().Be((2, 1));   // 1 row again: the first attempt's update was rolled back
        (await NamesAsync()).Should().Equal(Edited);
    }

    [Fact]
    public async Task Should_RefuseOpen_WhenTheProviderRetries()
    {
        await using var scope = RetryingScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();

        var open = () => items.OpenMassiveAsync(Ct);

        await open.Should().ThrowAsync<InvalidOperationException>().WithMessage("*RunMassiveAsync*");
    }

    private sealed class TransientFailureException : Exception;

    // A provider that retries, as PostgreSQL's EnableRetryOnFailure: a transaction opened outside the strategy is refused.
    private sealed class RetryingStrategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, maxRetryCount: 3, TimeSpan.FromMilliseconds(1))
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is TransientFailureException;

        protected override TimeSpan? GetNextDelay(Exception lastException) => base.GetNextDelay(lastException) is null ? null : TimeSpan.Zero;
    }

    private sealed class RetryingStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new RetryingStrategy(dependencies);
    }
}
