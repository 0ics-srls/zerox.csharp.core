using Zerox.Core.Identity;

namespace Zerox.Core.Tests.Identity;

public sealed class HierIdTests
{
    private static readonly string[] P1P2 = ["P1", "P2"];

    [Fact]
    public void Test_Parse()
    {
        var h = HierId.Parse("P1/P2");
        Assert.Equal('/', h.Separator);
        Assert.Equal("P1", h.Parts[0]);
        Assert.Equal("P2", h.Parts[1]);

        var h1 = HierId.Parse("P1\\P2", '\\');
        Assert.Equal('\\', h1.Separator);
        Assert.Equal("P1", h1.Parts[0]);
        Assert.Equal("P2", h1.Parts[1]);
    }

    [Fact]
    public void Test_From()
    {
        var h = HierId.From(P1P2.AsEnumerable());
        Assert.Equal('/', h.Separator);
        Assert.Equal(["P1", "P2"], h.Parts);

        var h1 = HierId.From(P1P2.AsEnumerable(), '\\');
        Assert.Equal('\\', h1.Separator);
        Assert.Equal(["P1", "P2"], h1.Parts);

        var h2 = HierId.From(h1, '$');
        Assert.Equal('$', h2.Separator);
        Assert.Equal(["P1", "P2"], h2.Parts);

        var h3 = HierId.From(h);
        Assert.Equal('/', h3.Separator);
        Assert.Equal(["P1", "P2"], h3.Parts);

        var h4 = HierId.From("P1", "P2");
        Assert.Equal('/', h4.Separator);
        Assert.Equal(["P1", "P2"], h4.Parts);
    }

    [Fact]
    public void Test_ToString()
    {
        Assert.Equal("P1/P2", HierId.From("P1", "P2").ToString());
        var h3 = HierId.From("P1", "P2", "P3");
        Assert.Equal("P1/P2/P3", h3.ToString(-1));
        Assert.Equal("P1/P2/P3", h3.ToString(0));
        Assert.Equal("P1", h3.ToString(1));
        Assert.Equal("P1/P2", h3.ToString(2));
        Assert.Equal("P1/P2/P3", h3.ToString(3));
        Assert.Equal("P1/P2/P3", h3.ToString(4));
    }

    [Fact]
    public void Test_Operator()
    {
        var h1 = HierId.From();
        Assert.Equal("", h1.ToString());
        h1 += "P1";
        h1 += "P2";
        Assert.Equal("P1/P2", h1.ToString());
        var h2 = h1 + "P3";
        Assert.Equal("P1/P2/P3", h2.ToString());
        h2 -= "P2";
        Assert.Equal("P1/P3", h2.ToString());
        var ha = HierId.From("P1");
        var hb = HierId.From("P2");
        var hc = ha + hb;
        Assert.Equal("P1/P2", hc.ToString());
        Assert.Equal("P1", ha.ToString());    // a sum does not modify its addends
        Assert.Equal("P2", hb.ToString());
    }

    [Fact]
    public void Test_Operators_eq_neq()
    {
        var h1 = HierId.Parse("P1/P2");
        var h11 = HierId.From("P1", "P2");
        var h2 = HierId.Parse("P1\\P2", '\\');
        var h3 = HierId.Parse("P1/P2/P3");
        Assert.True(h1 == h11);
        Assert.True(h1 != h2);
        Assert.True(h1 != h3);
        Assert.True(h2 != h3);
        Assert.True(h1 == "P1/P2");
        Assert.True(h1 != "P1/P2/P3");
        Assert.True(h1 != "P1\\P2"); // the separator cannot be told from a string
    }

    [Fact]
    public void Test_GetNumberOfEndingMatchingParts()
    {
        var h1 = HierId.Parse("P1/P2/P3");
        var h11 = HierId.From("P0", "P1", "P2", "P3");
        var h2 = HierId.Parse("P2/P1/P4");
        var h3 = HierId.Parse("P1/P2/P4");
        Assert.Equal(3, h1.GetNumberOfEndingMatchingParts(h11));
        Assert.Equal(1, h2.GetNumberOfEndingMatchingParts(h3));
        Assert.Equal(0, h1.GetNumberOfEndingMatchingParts(h3));
    }

    [Fact]
    public void GetParts()
    {
        var h4 = HierId.From("P0", "P1", "P2", "P3");
        var h3 = HierId.From("P0", "P1", "P2");
        var h2 = HierId.From("P0", "P1");
        var h1 = HierId.From("P0");
        Assert.True(h4.GetParts(3) == h3);
        Assert.True(h4.GetParts(2) == h2);
        Assert.True(h4.GetParts(1) == h1);
        Assert.True(h4.GetParts(0) == h4);
        Assert.True(h1.GetParts(1) == h1);
        Assert.True(h2.GetParts(2) == h2);
        Assert.True(h3.GetParts(3) == h3);
        Assert.True(h4.GetParts(4) == h4);
    }

    [Fact]
    public void Test_Splice()
    {
        var h1 = HierId.Parse("P1/P2/P3");
        var h11 = HierId.From("P0", "P1", "P2", "P3");
        var h2 = HierId.Parse("P2/P1/P4");
        var h3 = HierId.Parse("P1/P2/P4");
        Assert.True(h1 == h11.Splice(1));
        Assert.True(h2.Splice(2) == h3.Splice(2));
    }

    [Fact]
    public void Test_Swap_Prefix()
    {
        var h1 = HierId.Parse("P1/P2/P3");
        var h11 = HierId.From("P00", "P10");
        var h2 = HierId.Parse("P00/P10/P3"); // the first two parts swapped
        Assert.True(h2 == h1.CopyWithNewPrefix(h11));
    }

    [Fact]
    public void Test_StartsWith()
    {
        var h1 = HierId.Parse("P1/P2/P3");
        var h11 = HierId.From("P1", "P2", "P3", "P4");
        var h2 = HierId.Parse("P2/P1");
        var h3 = HierId.Parse("P1/P2/P4");
        var h4 = HierId.Parse("PX/P2/PY");
        var h5 = HierId.Empty();
        Assert.True(h11.StartsWith(h1));
        Assert.True(h11.StartsWith(h5));
        Assert.False(h11.StartsWith(h2));
        Assert.False(h11.StartsWith(h3));
        Assert.False(h11.StartsWith(h4));
        Assert.False(h1.StartsWith(h11));
        Assert.False(h1.StartsWith(h2));
        Assert.False(h1.StartsWith(h3));
        Assert.False(h1.StartsWith(h4));
        Assert.True(h1.StartsWith(h1));
        Assert.True(h11.StartsWith(h11));
        Assert.True(h5.StartsWith(h5));
    }

    [Fact]
    public void Test_Operators_partial_pattern()
    {
        var h1 = HierId.Parse("P1/P2/P3");
        var h11 = HierId.From("P1", "P2", "P3", "P4");
        var h2 = HierId.Parse("P2/P1");
        var h3 = HierId.Parse("P1/P2/P4");
        var h4 = HierId.Parse("PX/P2/PY");
        var h5 = HierId.Empty();
        Assert.Equal(3, h1.GetSimilarRootSize(h11));
        Assert.Equal(3, h11.GetSimilarRootSize(h1));
        Assert.Equal(0, h1.GetSimilarRootSize(h2));
        Assert.Equal(2, h1.GetSimilarRootSize(h3));
        Assert.Equal(0, h1.GetSimilarRootSize(h5));
        Assert.Equal(0, h5.GetSimilarRootSize(h1));
        // pattern
        Assert.True(h1.MatchPartsByPatternPms(h11, true, true, true));
        Assert.True(h1.MatchPartsByPatternPms(h11, true, true, false));
        Assert.True(h1.MatchPartsByPatternPms(h11, true, false, false));
        Assert.True(h1.MatchPartsByPatternPms(h11, false, true, true));
        Assert.True(h1.MatchPartsByPatternPms(h11, false, false, true));
        Assert.True(h1.MatchPartsByPatternPms(h11, false, false, false));
        Assert.True(h1.MatchPartsByPatternPms(h11, true, true));
        Assert.True(h1.MatchPartsByPatternPms(h11, false, true));
        Assert.True(h1.MatchPartsByPatternPms(h11, true, false));
        Assert.True(h1.MatchPartsByPatternPms(h11, false, false));
        Assert.True(h1.MatchPartsByPatternPms(h3, true, true, false));
        Assert.True(h1.MatchPartsByPatternPms(h3, true, false, false));
        Assert.True(h1.MatchPartsByPatternPms(h3, false, true, false));
        Assert.True(h1.MatchPartsByPatternPms(h3, false, false, false));
        Assert.False(h1.MatchPartsByPatternPms(h3, true, true, true));
        Assert.False(h1.MatchPartsByPatternPms(h3, true, false, true));
        Assert.False(h1.MatchPartsByPatternPms(h3, false, true, true));
        Assert.False(h1.MatchPartsByPatternPms(h3, false, false, true));
        Assert.False(h1.MatchPartsByPatternPms(h4, true, true, false));
        Assert.False(h1.MatchPartsByPatternPms(h4, true, false, false));
        Assert.True(h1.MatchPartsByPatternPms(h4, false, true, false));
        Assert.True(h1.MatchPartsByPatternPms(h4, false, false, false));
        Assert.False(h1.MatchPartsByPatternPms(h4, true, true, true));
        Assert.False(h1.MatchPartsByPatternPms(h4, true, false, true));
        Assert.False(h1.MatchPartsByPatternPms(h4, false, true, true));
        Assert.False(h1.MatchPartsByPatternPms(h4, false, false, true));
    }

    [Fact]
    public void Should_LeaveTheIdUnchanged_WhenAPartIsAddedOrRemoved()
    {
        var id = HierId.From("P1", "P2");

        var added = id + "x";
        var removed = id - "P1";
        var appended = id.Append("x");

        id.ToString().Should().Be("P1/P2");
        added.ToString().Should().Be("P1/P2/x");
        removed.ToString().Should().Be("P2");
        appended.ToString().Should().Be("P1/P2/x");
    }

    [Fact]
    public void Should_KeepTheSeparator_WhenTakingTheFirstParts()
        => HierId.Parse("a$b$c", '$').GetParts(2).ToString().Should().Be("a$b");

    [Fact]
    public void Should_SpliceToTheSameOrToEmpty_WhenNIsZeroOrBeyond()
    {
        var id = HierId.From("a", "b", "c");

        (id.Splice(0) == id).Should().BeTrue();
        id.Splice(3).Count.Should().Be(0);
        id.Splice(9).Count.Should().Be(0);
    }

    [Fact]
    public void Should_HashEqually_WhenIdsAreEqual()
    {
        var first = HierId.Parse("a/b/c");
        var second = HierId.From("a", "b", "c");
        var map = new Dictionary<HierId, int> { [first] = 1 };

        first.GetHashCode().Should().Be(second.GetHashCode());
        map[second].Should().Be(1);
    }

    [Fact]
    public void Name_AnIdOfThreeParts_IsItsLastPart()
        => HierId.Parse("a/b/c").Name.Should().Be("c");

    [Fact]
    public void Name_AnEmptyId_IsEmpty()
        => HierId.Empty().Name.Should().BeEmpty();

    [Fact]
    public void Parent_AnIdOfThreeParts_IsItsFirstTwo()
        => HierId.Parse("a/b/c").Parent.Should().Be(HierId.Parse("a/b"));

    [Fact]
    public void Parent_AnIdOfOnePart_IsTheEmptyIdWithItsSeparator()
    {
        var parent = HierId.Parse("a", '$').Parent;

        parent.Count.Should().Be(0);
        parent.Separator.Should().Be('$');
    }

    [Fact]
    public void Parent_AnEmptyId_IsEmpty()
        => HierId.Empty().Parent.Count.Should().Be(0);
}
