using Zerox.Core.Collections;

namespace Zerox.Core.Tests.Collections;

public sealed class CircularLinkedListTests
{
    private sealed record SomeData(int Val);

    [Fact]
    public void Test_Exception()
    {
        var list = new CircularLinkedList<SomeData>();
        list.Add(new SomeData(1));

        Assert.Throws<ArgumentException>(() => list.Add(new SomeData(1)));
    }

    [Fact]
    public void Test_TryAdd()
    {
        var list = new CircularLinkedList<SomeData>();
        list.Add(new SomeData(1));

        Assert.False(list.TryAdd(new SomeData(1)));
        Assert.True(list.TryAdd(new SomeData(2)));
    }

    [Fact]
    public void Test_limits()
    {
        var list = new CircularLinkedList<SomeData>();
        // remove on an empty list is fine
        list.Remove(new SomeData(1));
        // the current item is null
        Assert.Null(list.Peek());
        // Next on an empty list gives null
        Assert.Null(list.Next());
        list.Add(new SomeData(1));
        // Next on one item returns it every time
        Assert.Equal(list.Next(), list.Next());
        list.Clear();
        const int count = 1000;
        for (var i = 0; i < count; ++i)
        {
            list.Add(new SomeData(i));
            Assert.Equal(i + 1, list.Count);
        }
        for (var i = 0; i < count; ++i)
            Assert.Equal(i, list.Next()?.Val);
        // the last Next went back to the start
        Assert.Equal(0, list.Peek()?.Val);
    }

    [Fact]
    public void Test_Enum()
    {
        var list = new CircularLinkedList<SomeData>();
        const int count = 1000;
        for (var i = 0; i < count; ++i)
        {
            list.Add(new SomeData(i));
            Assert.Equal(i + 1, list.Count);
        }

        var items = list.ToList();

        for (var i = 0; i < count; ++i)
            Assert.Equal(i, items[i].Val);
    }

    [Fact]
    public void Test_Create()
    {
        var list = new CircularLinkedList<SomeData>();
        const int count = 1000;
        for (var i = 0; i < count; ++i)
        {
            list.Add(new SomeData(i));
            Assert.Equal(i + 1, list.Count);
        }
        for (var i = 0; i < count; ++i)
        {
            Assert.Equal(count - i, list.Count);
            list.Remove(new SomeData(i));
        }
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void Should_MoveTheCursorToTheNextItem_WhenTheCurrentItemIsRemoved()
    {
        var list = new CircularLinkedList<SomeData>();
        list.Add(new SomeData(1));
        list.Add(new SomeData(2));
        list.Add(new SomeData(3));
        list.Next();
        list.Peek().Should().Be(new SomeData(2));

        list.Remove(new SomeData(2));

        list.Peek().Should().Be(new SomeData(3));
    }

    [Fact]
    public void Should_ReturnFalse_WhenRemovingAnAbsentItem()
    {
        var list = new CircularLinkedList<SomeData>();
        list.Add(new SomeData(1));

        list.Remove(new SomeData(7)).Should().BeFalse();
        list.Count.Should().Be(1);
    }
}
