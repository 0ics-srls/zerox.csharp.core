
namespace Zerox.Dnf.Tests;

public class DnfConverterTests
{
    [Fact]
    public void Convert_SingleBitRef_OneTerm()
    {
        var expr = BoolExprParser.Parse("C1.F0");
        var dnf = DnfConverter.Convert(expr);

        Assert.Single(dnf.Terms);
        Assert.Equal(1UL, dnf.Terms[0].MustBeSet);
        Assert.Equal(0UL, dnf.Terms[0].MustBeClear);
    }

    [Fact]
    public void Convert_And_OneTermWithBothBits()
    {
        var expr = BoolExprParser.Parse("C1.F0 & C2.F0");
        var dnf = DnfConverter.Convert(expr);

        Assert.Single(dnf.Terms);
        // C1.F0 = bit 0, C2.F0 = bit 16
        Assert.Equal((1UL << 0) | (1UL << 16), dnf.Terms[0].MustBeSet);
        Assert.Equal(0UL, dnf.Terms[0].MustBeClear);
    }

    [Fact]
    public void Convert_Or_TwoTerms()
    {
        var expr = BoolExprParser.Parse("C1.F0 | C2.F0");
        var dnf = DnfConverter.Convert(expr);

        Assert.Equal(2, dnf.Terms.Count);
        Assert.Contains(dnf.Terms, t => t.MustBeSet == 1UL);      // C1.F0
        Assert.Contains(dnf.Terms, t => t.MustBeSet == (1UL << 16)); // C2.F0
    }

    [Fact]
    public void Convert_Not_SetsMustBeClear()
    {
        var expr = BoolExprParser.Parse("!C1.F0");
        var dnf = DnfConverter.Convert(expr);

        Assert.Single(dnf.Terms);
        Assert.Equal(0UL, dnf.Terms[0].MustBeSet);
        Assert.Equal(1UL, dnf.Terms[0].MustBeClear);
    }

    [Fact]
    public void Convert_AndWithNot_CombinesMasks()
    {
        // C1.F0 & !C2.F0
        var expr = BoolExprParser.Parse("C1.F0 & !C2.F0");
        var dnf = DnfConverter.Convert(expr);

        Assert.Single(dnf.Terms);
        Assert.Equal(1UL, dnf.Terms[0].MustBeSet);          // C1.F0 must be set
        Assert.Equal(1UL << 16, dnf.Terms[0].MustBeClear);  // C2.F0 must be clear
    }

    [Fact]
    public void Convert_Complex_ExpandsToDnf()
    {
        // C1.F1 & (C2.F2 | C2.F3) = (C1.F1 & C2.F2) | (C1.F1 & C2.F3)
        var expr = BoolExprParser.Parse("C1.F1 & (C2.F2 | C2.F3)");
        var dnf = DnfConverter.Convert(expr);

        Assert.Equal(2, dnf.Terms.Count);

        // Term 1: C1.F1 & C2.F2 = bit 1 + bit 18
        var term1Mask = (1UL << 1) | (1UL << 18);
        // Term 2: C1.F1 & C2.F3 = bit 1 + bit 19
        var term2Mask = (1UL << 1) | (1UL << 19);

        Assert.Contains(dnf.Terms, t => t.MustBeSet == term1Mask);
        Assert.Contains(dnf.Terms, t => t.MustBeSet == term2Mask);
    }

    [Fact]
    public void Convert_DeMorgan_NotAnd()
    {
        // !(C1.F0 & C2.F0) = !C1.F0 | !C2.F0
        var expr = BoolExprParser.Parse("!(C1.F0 & C2.F0)");
        var dnf = DnfConverter.Convert(expr);

        Assert.Equal(2, dnf.Terms.Count);
        Assert.Contains(dnf.Terms, t => t.MustBeClear == 1UL);        // !C1.F0
        Assert.Contains(dnf.Terms, t => t.MustBeClear == (1UL << 16)); // !C2.F0
    }

    [Fact]
    public void Convert_ConstantTrue_Tautology()
    {
        var expr = BoolExprParser.Parse("1");
        var dnf = DnfConverter.Convert(expr);

        Assert.True(dnf.IsTrue);
        Assert.Single(dnf.Terms);
        Assert.True(dnf.Terms[0].IsTautology);
    }

    [Fact]
    public void Convert_ConstantFalse_Empty()
    {
        var expr = BoolExprParser.Parse("0");
        var dnf = DnfConverter.Convert(expr);

        Assert.True(dnf.IsFalse);
        Assert.Empty(dnf.Terms);
    }

    [Fact]
    public void Convert_OrWithTrue_Simplifies()
    {
        // C1.F0 | 1 = 1
        var expr = BoolExprParser.Parse("C1.F0 | 1");
        var dnf = DnfConverter.Convert(expr);

        Assert.True(dnf.IsTrue);
    }

    [Fact]
    public void Convert_AndWithFalse_Simplifies()
    {
        // C1.F0 & 0 = 0
        var expr = BoolExprParser.Parse("C1.F0 & 0");
        var dnf = DnfConverter.Convert(expr);

        Assert.True(dnf.IsFalse);
    }

    [Fact]
    public void Convert_Contradiction_Removed()
    {
        // C1.F0 & !C1.F0 = contradiction = false
        var expr = BoolExprParser.Parse("C1.F0 & !C1.F0");
        var dnf = DnfConverter.Convert(expr);

        Assert.True(dnf.IsFalse);
    }

    [Fact]
    public void Convert_DoubleNot_Cancels()
    {
        // !!C1.F0 = C1.F0
        var expr = BoolExprParser.Parse("!!C1.F0");
        var dnf = DnfConverter.Convert(expr);

        Assert.Single(dnf.Terms);
        Assert.Equal(1UL, dnf.Terms[0].MustBeSet);
        Assert.Equal(0UL, dnf.Terms[0].MustBeClear);
    }
}
