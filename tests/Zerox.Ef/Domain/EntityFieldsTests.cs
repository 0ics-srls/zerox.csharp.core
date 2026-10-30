namespace Zerox.Ef.Domain.Tests;

public sealed class EntityFieldsTests
{
    [Fact]
    public void Should_NameTheEditableFields_WhenAsked()
    {
        var names = EntityFields.Editable<Item>();

        names.Should().Contain([nameof(Item.Name), nameof(Item.Quantity), nameof(Item.Note), nameof(Item.Kind)]);
        names.Should().NotContain([nameof(Item.Id), nameof(Item.Box), nameof(Item.Secret)]);
    }

    [Fact]
    public void Should_NameNoCollectionNavigation_WhenAsked()
    {
        var names = EntityFields.Editable<Box>();

        names.Should().Contain(nameof(Box.Label));
        names.Should().NotContain([nameof(Box.Items), nameof(Box.Id)]);
    }

}
