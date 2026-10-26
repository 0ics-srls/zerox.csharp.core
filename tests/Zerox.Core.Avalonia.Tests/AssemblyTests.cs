using System.Reflection;
using Zerox.Tests.Shared;

namespace Zerox.Core.Avalonia.Tests;

public sealed class AssemblyTests
{
    private static readonly Assembly ZeroxCoreAvalonia = Assembly.Load(new AssemblyName("Zerox.Core.Avalonia"));

    [Fact]
    public void Should_TakeTheVersionFromVersionBuild_WhenZeroxCoreAvaloniaIsBuilt()
        => ZeroxCoreAvalonia.GetName().Version.Should().Be(new Version(1, 0, RepositoryRoot.VersionBuild(), 0));
}
