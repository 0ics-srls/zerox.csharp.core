namespace Zerox.Ef.Domain.Tests;

public sealed class EntityFieldsTests
{
    [Fact]
    public void Should_CopyTheEditableFields_WhenCopied()
    {
        var target = new Item { Id = 1, Name = "old", Quantity = 1, Note = "old", Kind = ItemKind.Small };
        var source = new Item { Id = 1, Name = "new", Quantity = 5, Note = null, Kind = ItemKind.Large };

        EntityFields.CopyEditable(source, target);

        target.Name.Should().Be("new");
        target.Quantity.Should().Be(5);
        target.Note.Should().BeNull();
        target.Kind.Should().Be(ItemKind.Large);
    }

    [Fact]
    public void Should_KeepTheReservedFields_WhenCopied()
    {
        var target = new Item { Id = 1, Secret = "kept" };

        EntityFields.CopyEditable(new Item { Id = 1, Secret = "" }, target);

        target.Secret.Should().Be("kept");
    }

    [Fact]
    public void Should_KeepTheNavigationsAndTheKey_WhenCopied()
    {
        var box = new Box { Id = 7, Label = "kept" };
        var target = new Box { Id = 7, Label = "old", Items = [new Item { Name = "kept" }] };
        var item = new Item { Id = 1, Box = box };

        EntityFields.CopyEditable(new Box { Id = 8, Label = "new", Items = [] }, target);
        EntityFields.CopyEditable(new Item { Id = 1, Box = null }, item);

        target.Id.Should().Be(7);
        target.Label.Should().Be("new");
        target.Items.Should().ContainSingle();
        item.Box.Should().BeSameAs(box);
    }

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

    // One selection, not two: the names are exactly the properties CopyEditable writes.
    [Fact]
    public void Should_NameTheFieldsCopyEditableWrites_WhenAsked()
    {
        var source = new Item
        {
            Id = 7,
            Name = "n",
            Quantity = 5,
            Price = 2.5m,
            Kind = ItemKind.Large,
            At = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Paid = true,
            Note = "note",
            OwnerId = 3,
            Secret = "s",
            BoxId = 4,
            Box = new Box { Id = 4 },
        };
        var target = new Item();
        var untouched = new Item();
        EntityFields.CopyEditable(source, target);
        var written = typeof(Item).GetProperties()
            .Where(p => p.SetMethod is { IsPublic: true })
            .Where(p => Equals(p.GetValue(target), p.GetValue(source)) && !Equals(p.GetValue(untouched), p.GetValue(source)))
            .Select(p => p.Name);

        var names = EntityFields.Editable<Item>();

        names.Should().BeEquivalentTo(written);
    }
}
