using Zerox.Core.Reflection;

namespace Zerox.Core.Tests.Reflection;

public sealed class AttributeExtensionsTests
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, Inherited = true)]
    public sealed class MarkAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }

    [Mark("child type")]
    public sealed class Child;

    public class Marked
    {
        [Mark("first")]
        public int First { get; set; }

        public int Plain { get; set; }

        [Mark("second")]
        public virtual string Second { get; set; } = "";

        public Child Child { get; set; } = new();
    }

    public sealed class Derived : Marked
    {
        public override string Second { get; set; } = "";
    }

    [Fact]
    public void Should_ListThePropertiesWithTheAttribute_WhenTheTypeHasThem()
        => typeof(Marked).GetPropertiesWith<MarkAttribute>()
            .Select(found => (found.Property.Name, found.Attribute.Name))
            .Should().Equal(("First", "first"), ("Second", "second"));

    [Fact]
    public void Should_IncludeInheritedAttributes_WhenTheBaseTypeMarksAProperty()
        => typeof(Derived).GetPropertiesWith<MarkAttribute>()
            .Where(found => found.Property.Name == "Second")
            .Should().ContainSingle().Which.Attribute.Name.Should().Be("second");

    [Fact]
    public void Should_FindTheAttributeOfANamedProperty_WhenItExists()
    {
        typeof(Marked).GetPropertyAttribute<MarkAttribute>("First")!.Name.Should().Be("first");
        typeof(Marked).GetPropertyAttribute<MarkAttribute>("Nope").Should().BeNull();
    }

    [Fact]
    public void Should_FindTheAttributeOfThePropertyType_WhenTheTypeIsMarked()
        => typeof(Marked).GetAttributeOfPropertyType<MarkAttribute>("Child")!.Name.Should().Be("child type");
}
