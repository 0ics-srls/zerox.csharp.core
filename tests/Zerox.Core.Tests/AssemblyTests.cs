using System.Globalization;
using System.Reflection;
using Zerox.Tests.Shared;

namespace Zerox.Core.Tests;

public sealed class AssemblyTests
{
    private static readonly Assembly ZeroxCore = Assembly.Load(new AssemblyName("Zerox.Core"));

    [Fact]
    public void Should_TakeTheVersionFromVersionBuild_WhenZeroxCoreIsBuilt()
        => ZeroxCore.GetName().Version.Should().Be(new Version(1, 0, RepositoryRoot.VersionBuild(), 0));

}

public sealed class RepositoryRootTests
{
    [Fact]
    public void Should_FindTheRepositoryRoot_WhenRunFromTheTestOutput()
    {
        File.Exists(Path.Combine(RepositoryRoot.Folder, "zerox.csharp.core.slnx")).Should().BeTrue();
        RepositoryRoot.File("version.build").Should().Be(Path.Combine(RepositoryRoot.Folder, "version.build"));
        File.Exists(RepositoryRoot.File("version.build")).Should().BeTrue();
    }

    [Fact]
    public void Should_ReadTheBuildNumber_WhenVersionBuildHoldsANumber()
    {
        var expected = int.Parse(File.ReadAllText(Path.Combine(RepositoryRoot.Folder, "version.build")).Trim(), CultureInfo.InvariantCulture);

        RepositoryRoot.VersionBuild().Should().Be(expected).And.BeGreaterThanOrEqualTo(1);
    }
}
