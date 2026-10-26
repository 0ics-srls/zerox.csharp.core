using Zerox.Core.Editing;

namespace Zerox.Core.Tests.Editing;

public sealed class PocoExtensionsTests
{
    internal sealed class Inner
    {
        public int X;
    }

    internal sealed class Sample
    {
        public string Name = "";
        private int _secret;
        public List<int> Values = [];
        public Inner Child = new();

        public int Secret => _secret;

        public Sample WithSecret(int secret)
        {
            _secret = secret;
            return this;
        }
    }

    internal sealed class ById(int id, string name) : IComparable<ById>
    {
        public int Id { get; } = id;

        public string Name { get; } = name;

        public int CompareTo(ById? other) => other is null ? 1 : Id.CompareTo(other.Id);
    }

    private static Sample Original() => new Sample { Name = "a", Values = [1, 2], Child = new Inner { X = 3 } }.WithSecret(7);

    [Fact]
    public void Should_CloneDeeply_WhenCloningByFields()
    {
        var original = Original();

        var clone = original.CloneByFields();

        clone.Name.Should().Be("a");
        clone.Secret.Should().Be(7);
        clone.Values.Should().Equal(1, 2);
        clone.Values.Should().NotBeSameAs(original.Values);
        clone.Child.Should().NotBeSameAs(original.Child);
        clone.Child.X = 9;
        original.Child.X.Should().Be(3);
    }

    [Fact]
    public void Should_RejectNull_WhenCloning()
        => FluentActions.Invoking(() => PocoExtensions.CloneByFields<Sample>(null!)).Should().Throw<ArgumentNullException>();

    [Fact]
    public void Should_ReportNoChange_WhenTheCopyIsEqual()
    {
        var original = Original();

        original.IsChanged(original.CloneByFields()).Should().BeFalse();
    }

    [Fact]
    public void Should_ListTheDifferences_WhenAMemberDiffers()
    {
        var original = Original();
        var copy = original.CloneByFields();
        copy.Child.X = 9;

        original.IsChanged(copy, out var differences).Should().BeTrue();

        differences.Should().ContainSingle().Which.Should().Be(new MemberDifference("Child.X", "3", "9"));
    }

    [Fact]
    public void Should_UseCompareTo_WhenTheTypeIsComparable()
        => new ById(1, "a").IsChanged(new ById(1, "b")).Should().BeFalse();

    [Fact]
    public void Should_DecideOnNulls_WhenOneOrBothAreNull()
    {
        ((Sample?)null).IsChanged(null).Should().BeFalse();
        Original().IsChanged(null).Should().BeTrue();
        ((Sample?)null).IsChanged(Original()).Should().BeTrue();
    }
}
