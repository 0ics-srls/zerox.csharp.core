namespace Zerox.Ef.Domain.Tests;

public sealed class StaleVersionExceptionTests
{
    [Fact]
    public void Should_CarryTheReason_WhenAStaleVersionIsThrown()
    {
        var stale = new StaleVersionException("Task 5 changed since it was read.");

        stale.Message.Should().Be("Task 5 changed since it was read.");
        new StaleVersionException().Message.Should().NotBeNullOrWhiteSpace();
    }
}
