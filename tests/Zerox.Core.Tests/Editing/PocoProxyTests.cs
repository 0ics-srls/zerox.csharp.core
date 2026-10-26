using System.Text.Json;
using Zerox.Core.Editing;

namespace Zerox.Core.Tests.Editing;

public sealed class PocoProxyTests
{
    internal sealed class SomeData
    {
        public string Name = string.Empty;
        public ushort HueMin;
        public uint AreaMin;
        public ushort DustFlt;
        public bool Outside;
    }

    internal sealed class SomeDataProxy : PocoProxy<SomeData>
    {
        public SomeDataProxy()
        {
        }

        public SomeDataProxy(SomeDataProxy data)
            : base(data)
        {
        }

        public string Name
        {
            get => Data.Name;
            set => SetProperty(ref Data.Name, value);
        }

        public ushort HueMin
        {
            get => Data.HueMin;
            set => SetProperty(ref Data.HueMin, value);
        }

        public uint AreaMin
        {
            get => Data.AreaMin;
            set => SetProperty(ref Data.AreaMin, value);
        }

        public ushort DustFlt
        {
            get => Data.DustFlt;
            set => SetProperty(ref Data.DustFlt, value);
        }

        public bool Outside
        {
            get => Data.Outside;
            set => SetProperty(ref Data.Outside, value);
        }

        public void SaveName() => SaveStateOf(nameof(SomeData.Name));
    }

    internal sealed class SomeDataProps
    {
        public string Name { get; set; } = string.Empty;
        public ushort HueMin { get; set; }
        public uint AreaMin { get; set; }
        public ushort DustFlt { get; set; }
        public bool Outside { get; set; }
    }

    internal sealed class SomeDataPropsProxy : PocoProxy<SomeDataProps>
    {
        public SomeDataPropsProxy()
        {
        }

        public SomeDataPropsProxy(SomeDataPropsProxy data)
            : base(data)
        {
        }

        public string Name
        {
            get => Data.Name;
            set => SetProperty(x => x.Name, value);
        }

        public ushort HueMin
        {
            get => Data.HueMin;
            set => SetProperty(x => x.HueMin, value);
        }

        public uint AreaMin
        {
            get => Data.AreaMin;
            set => SetProperty(x => x.AreaMin, value);
        }

        public ushort DustFlt
        {
            get => Data.DustFlt;
            set => SetProperty(x => x.DustFlt, value);
        }

        public bool Outside
        {
            get => Data.Outside;
            set => SetProperty(x => x.Outside, value);
        }

        public void SetThroughAMethod(string value) => SetProperty(x => x.ToString(), value);
    }

    private static SomeDataProxy A()
    {
        var a = new SomeDataProxy { Name = "A", HueMin = 1, AreaMin = 2, DustFlt = 3, Outside = true };
        a.SaveState(); // not changed!
        return a;
    }

    private static SomeDataPropsProxy Ap()
    {
        var ap = new SomeDataPropsProxy { Name = "A", HueMin = 1, AreaMin = 2, DustFlt = 3, Outside = true };
        ap.SaveState(); // not changed!
        return ap;
    }

    private static List<string?> Listen(System.ComponentModel.INotifyPropertyChanged source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    [Fact]
    public void Test_Changed()
    {
        var a = A();
        Assert.False(a.IsChanged);
        a.Name = "Pippo";
        Assert.True(a.IsChanged);
        a.Name = "A";
        Assert.False(a.IsChanged);
        a.Name = "Pippo";
        a.SaveState();
        Assert.False(a.IsChanged);
        a.ResetState();
        Assert.False(a.IsChanged);
        var b = new SomeDataProxy(a);
        a.Name = "pippo";
        Assert.True(a.IsChanged);
        Assert.False(b.IsChanged); // b is a clone: it does not follow a
        Assert.Equal("Pippo", b.Name);
        Assert.False(a.DataEquals(b));
        b.Name = a.Name;
        Assert.True(a.DataEquals(b));

        var ap = Ap();
        Assert.False(ap.IsChanged);
        ap.Name = "Pippo";
        Assert.True(ap.IsChanged);
        ap.Name = "A";
        Assert.False(ap.IsChanged);
        ap.Name = "Pippo";
        ap.SaveState();
        Assert.False(ap.IsChanged);
        ap.ResetState();
        Assert.False(ap.IsChanged);
        var bp = new SomeDataPropsProxy(ap);
        ap.Name = "pippo";
        Assert.True(ap.IsChanged);
        Assert.False(bp.IsChanged);
        Assert.Equal("Pippo", bp.Name);
        Assert.False(ap.DataEquals(bp));
        bp.Name = ap.Name;
        Assert.True(ap.DataEquals(bp));
    }

    [Fact]
    public async Task Test_Copy_Stream()
    {
        var a = A();
        using var stream = new MemoryStream();

        await a.SerializeAsync(stream, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var b = new SomeDataProxy();
        await b.PopulateAsync(stream, TestContext.Current.CancellationToken);

        Assert.True(a.DataEquals(b));
    }

    [Fact]
    public void Test_Copy_String()
    {
        var a = A();
        var b = new SomeDataProxy();

        b.Populate(a.ToJson());

        Assert.True(a.DataEquals(b));
    }

    [Fact]
    public void Should_NotifyIsChanged_WhenAPropertyReallyChanges()
    {
        var a = A();
        var names = Listen(a);

        a.Name = "B";
        names.Should().Equal("Name", "IsChanged");

        names.Clear();
        a.Name = "B";
        names.Should().BeEmpty();
    }

    [Fact]
    public void Should_NotifyEveryProperty_WhenTheStateIsReset()
    {
        var a = A();
        a.Name = "B";
        var names = Listen(a);

        a.ResetState();
        names.Should().Equal([null]);

        names.Clear();
        a.SetState(new SomeData { Name = "C" });
        names.Should().Equal([null]);
        a.Name.Should().Be("C");
    }

    [Fact]
    public void Should_KeepTheData_WhenPopulateFails()
    {
        var a = A();
        a.Name = "B";

        FluentActions.Invoking(() => a.Populate("{ not json")).Should().Throw<JsonException>();
        a.Name.Should().Be("B");
        a.IsChanged.Should().BeTrue();
        FluentActions.Invoking(() => a.Populate("null")).Should().Throw<JsonException>();
        a.Name.Should().Be("B");
    }

    [Fact]
    public void Should_SaveOneMember_WhenSaveStateOfIsCalled()
    {
        var a = A();
        a.Name = "B";
        a.HueMin = 9;

        a.SaveName();
        a.IsChanged.Should().BeTrue();

        a.HueMin = 1;
        a.IsChanged.Should().BeFalse();
        a.Name.Should().Be("B");
    }

    [Fact]
    public void Should_ReturnACopy_WhenGetStateIsCalled()
    {
        var a = A();

        a.GetState().Name = "changed outside";

        a.Name.Should().Be("A");
        a.IsChanged.Should().BeFalse();
    }

    [Fact]
    public void Should_RejectTheExpression_WhenItSelectsNoProperty()
        => FluentActions.Invoking(() => Ap().SetThroughAMethod("x")).Should().Throw<ArgumentException>();
}
