using Zerox.Ef.Core;

namespace Zerox.Ef.Domain.Tests;

// The lock is the database's row version: nothing of the application writes a version, nothing pins one.
public sealed class SurfaceTests
{
    [Fact]
    public void Should_ExposeNoVersionType_WhenTheCoreIsInspected() =>
        typeof(IEntity).Assembly.GetType("Zerox.Ef.Core.IHasOptimisticLock").Should().BeNull();

    [Fact]
    public void Should_ExposeNoPin_WhenTheRepositoryIsInspected() =>
        typeof(IRepository<>).GetMethod("PinReadVersion").Should().BeNull();

    [Fact]
    public void Should_TakeNoClientVersion_WhenLastWriterWinsIsInspected() =>
        typeof(LastWriterWins).GetMethod(nameof(LastWriterWins.SaveAsync))!.GetParameters()
            .Should().NotContain(p => p.Name == "versionReadByClient");
}
