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
}
