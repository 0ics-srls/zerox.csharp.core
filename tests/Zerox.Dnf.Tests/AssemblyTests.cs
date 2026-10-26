using System.Reflection;
using Zerox.Tests.Shared;

namespace Zerox.Dnf.Tests;

public sealed class AssemblyTests
{
    private static readonly Assembly ZeroxDnf = Assembly.Load(new AssemblyName("Zerox.Dnf"));

    [Fact]
    public void Should_TakeTheVersionFromVersionBuild_WhenZeroxDnfIsBuilt()
        => ZeroxDnf.GetName().Version.Should().Be(new Version(1, 0, RepositoryRoot.VersionBuild(), 0));

    [Fact]
    public void Should_ReferenceOnlyTheFramework_WhenZeroxDnfIsBuilt()
        => ZeroxDnf.GetReferencedAssemblies().Select(name => name.Name!)
            .Should().OnlyContain(name => name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard");
}
