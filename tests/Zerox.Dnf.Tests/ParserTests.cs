
namespace Zerox.Dnf.Tests;

public class ParserTests
{
    [Theory]
    [InlineData("C1.F0", 1, 0)]
    [InlineData("C2.F5", 2, 5)]
    [InlineData("C3.F15", 3, 15)]
    [InlineData("C4.F0", 4, 0)]
    public void Parse_BitRef_CorrectCameraAndFilter(string expr, int expectedCam, int expectedFilter)
    {
        var result = BoolExprParser.Parse(expr);

        var bitRef = Assert.IsType<BitRefExpr>(result);
        Assert.Equal(expectedCam, bitRef.Camera);
        Assert.Equal(expectedFilter, bitRef.Filter);
    }

    [Fact]
    public void Parse_BitRef_CorrectBitIndex()
    {
        // C1.F0 = bit 0, C2.F0 = bit 16, C3.F0 = bit 32, C4.F0 = bit 48
        Assert.Equal(0, ((BitRefExpr)BoolExprParser.Parse("C1.F0")).BitIndex);
        Assert.Equal(16, ((BitRefExpr)BoolExprParser.Parse("C2.F0")).BitIndex);
        Assert.Equal(32, ((BitRefExpr)BoolExprParser.Parse("C3.F0")).BitIndex);
        Assert.Equal(48, ((BitRefExpr)BoolExprParser.Parse("C4.F0")).BitIndex);

        // C1.F15 = bit 15, C2.F15 = bit 31
        Assert.Equal(15, ((BitRefExpr)BoolExprParser.Parse("C1.F15")).BitIndex);
        Assert.Equal(31, ((BitRefExpr)BoolExprParser.Parse("C2.F15")).BitIndex);
    }

    [Fact]
    public void Parse_Constant_True()
    {
        var result = BoolExprParser.Parse("1");
        var constExpr = Assert.IsType<ConstExpr>(result);
        Assert.True(constExpr.Value);
    }

    [Fact]
    public void Parse_Constant_False()
    {
        var result = BoolExprParser.Parse("0");
        var constExpr = Assert.IsType<ConstExpr>(result);
        Assert.False(constExpr.Value);
    }

    [Fact]
    public void Parse_And_TwoOperands()
    {
        var result = BoolExprParser.Parse("C1.F0 & C2.F1");

        var andExpr = Assert.IsType<AndExpr>(result);
        Assert.IsType<BitRefExpr>(andExpr.Left);
        Assert.IsType<BitRefExpr>(andExpr.Right);
    }

    [Fact]
    public void Parse_Or_TwoOperands()
    {
        var result = BoolExprParser.Parse("C1.F0 | C2.F1");

        var orExpr = Assert.IsType<OrExpr>(result);
        Assert.IsType<BitRefExpr>(orExpr.Left);
        Assert.IsType<BitRefExpr>(orExpr.Right);
    }

    [Fact]
    public void Parse_Not_SingleOperand()
    {
        var result = BoolExprParser.Parse("!C1.F0");

        var notExpr = Assert.IsType<NotExpr>(result);
        Assert.IsType<BitRefExpr>(notExpr.Operand);
    }

    [Fact]
    public void Parse_Precedence_AndBeforeOr()
    {
        // C1.F0 | C2.F1 & C3.F2 should be: C1.F0 | (C2.F1 & C3.F2)
        var result = BoolExprParser.Parse("C1.F0 | C2.F1 & C3.F2");

        var orExpr = Assert.IsType<OrExpr>(result);
        Assert.IsType<BitRefExpr>(orExpr.Left);  // C1.F0
        Assert.IsType<AndExpr>(orExpr.Right);    // C2.F1 & C3.F2
    }

    [Fact]
    public void Parse_Parentheses_OverridePrecedence()
    {
        // (C1.F0 | C2.F1) & C3.F2
        var result = BoolExprParser.Parse("(C1.F0 | C2.F1) & C3.F2");

        var andExpr = Assert.IsType<AndExpr>(result);
        Assert.IsType<OrExpr>(andExpr.Left);     // (C1.F0 | C2.F1)
        Assert.IsType<BitRefExpr>(andExpr.Right); // C3.F2
    }

    [Fact]
    public void Parse_Complex_Expression()
    {
        // C1.F1 & (C2.F2 | C2.F3)
        var result = BoolExprParser.Parse("C1.F1 & (C2.F2 | C2.F3)");

        var andExpr = Assert.IsType<AndExpr>(result);
        var left = Assert.IsType<BitRefExpr>(andExpr.Left);
        Assert.Equal(1, left.Camera);
        Assert.Equal(1, left.Filter);

        var orExpr = Assert.IsType<OrExpr>(andExpr.Right);
        var orLeft = Assert.IsType<BitRefExpr>(orExpr.Left);
        var orRight = Assert.IsType<BitRefExpr>(orExpr.Right);
        Assert.Equal(2, orLeft.Camera);
        Assert.Equal(2, orLeft.Filter);
        Assert.Equal(2, orRight.Camera);
        Assert.Equal(3, orRight.Filter);
    }

    [Fact]
    public void Parse_Whitespace_Ignored()
    {
        var result1 = BoolExprParser.Parse("C1.F0&C2.F1");
        var result2 = BoolExprParser.Parse("C1.F0 & C2.F1");
        var result3 = BoolExprParser.Parse("  C1.F0   &   C2.F1  ");

        // All should produce the same AST
        Assert.IsType<AndExpr>(result1);
        Assert.IsType<AndExpr>(result2);
        Assert.IsType<AndExpr>(result3);
    }

    [Theory]
    [InlineData("C0.F0")]   // Camera 0 invalid
    [InlineData("C5.F0")]   // Camera 5 invalid
    [InlineData("C1.F16")]  // Filter 16 invalid
    [InlineData("C1.F-1")]  // Negative filter
    [InlineData("X1.F0")]   // Wrong prefix
    [InlineData("")]        // Empty
    [InlineData("C1.")]     // Incomplete
    [InlineData("&")]       // Operator only
    public void Parse_Invalid_ThrowsFormatException(string expr)
    {
        Assert.ThrowsAny<Exception>(() => BoolExprParser.Parse(expr));
    }

    // =========================================================================
    // BoolExpr.ToString() Tests - Roundtrip Parse → ToString → Parse
    // =========================================================================

    [Fact]
    public void ToString_BitRef_Roundtrip()
    {
        var ast = BoolExprParser.Parse("C1.F0");
        var str = ast.ToString();
        Assert.Equal("C1.F0", str);

        // Roundtrip
        var ast2 = BoolExprParser.Parse(str);
        Assert.Equal(ast, ast2);
    }

    [Fact]
    public void ToString_Constant_True()
    {
        var ast = BoolExprParser.Parse("1");
        Assert.Equal("1", ast.ToString());
    }

    [Fact]
    public void ToString_Constant_False()
    {
        var ast = BoolExprParser.Parse("0");
        Assert.Equal("0", ast.ToString());
    }

    [Fact]
    public void ToString_Not_Simple()
    {
        var ast = BoolExprParser.Parse("!C1.F0");
        var str = ast.ToString();
        Assert.Equal("!C1.F0", str);

        // Roundtrip
        var ast2 = BoolExprParser.Parse(str);
        Assert.IsType<NotExpr>(ast2);
    }

    [Fact]
    public void ToString_And_Simple()
    {
        var ast = BoolExprParser.Parse("C1.F0 & C2.F1");
        var str = ast.ToString();
        Assert.Equal("C1.F0 & C2.F1", str);

        // Roundtrip
        var ast2 = BoolExprParser.Parse(str);
        Assert.IsType<AndExpr>(ast2);
    }

    [Fact]
    public void ToString_Or_Simple()
    {
        var ast = BoolExprParser.Parse("C1.F0 | C2.F1");
        var str = ast.ToString();
        Assert.Equal("C1.F0 | C2.F1", str);

        // Roundtrip
        var ast2 = BoolExprParser.Parse(str);
        Assert.IsType<OrExpr>(ast2);
    }

    [Fact]
    public void ToString_AndOr_PreservesParentheses()
    {
        // AND has higher precedence, so OR inside AND needs parens
        var ast = BoolExprParser.Parse("C1.F0 & (C2.F1 | C2.F2)");
        var str = ast.ToString();
        Assert.Equal("C1.F0 & (C2.F1 | C2.F2)", str);

        // Roundtrip
        var ast2 = BoolExprParser.Parse(str);
        var and2 = Assert.IsType<AndExpr>(ast2);
        Assert.IsType<OrExpr>(and2.Right);
    }

    [Fact]
    public void ToString_OrAnd_NoParen()
    {
        // OR has lower precedence, so AND inside OR doesn't need parens
        var ast = BoolExprParser.Parse("C1.F0 | C2.F1 & C3.F2");
        var str = ast.ToString();
        // Output should NOT have parens around AND
        Assert.Equal("C1.F0 | C2.F1 & C3.F2", str);

        // Roundtrip
        var ast2 = BoolExprParser.Parse(str);
        Assert.IsType<OrExpr>(ast2);
    }

    [Fact]
    public void ToString_NotComplex_WrapsInParens()
    {
        var ast = BoolExprParser.Parse("!(C1.F0 & C2.F1)");
        var str = ast.ToString();
        Assert.Equal("!(C1.F0 & C2.F1)", str);

        // Roundtrip
        var ast2 = BoolExprParser.Parse(str);
        var not2 = Assert.IsType<NotExpr>(ast2);
        Assert.IsType<AndExpr>(not2.Operand);
    }

    [Theory]
    [InlineData("C1.F0")]
    [InlineData("C2.F15")]
    [InlineData("!C1.F0")]
    [InlineData("C1.F0 & C2.F1")]
    [InlineData("C1.F0 | C2.F1")]
    [InlineData("C1.F0 & (C2.F1 | C2.F2)")]
    [InlineData("(C1.F0 | C1.F1) & C2.F0")]
    [InlineData("!C1.F0 & C2.F1")]
    [InlineData("!(C1.F0 | C2.F1)")]
    [InlineData("C1.F0 & C2.F1 & C3.F2")]
    [InlineData("C1.F0 | C2.F1 | C3.F2")]
    public void ToString_Roundtrip_ParseableOutput(string expr)
    {
        var ast1 = BoolExprParser.Parse(expr);
        var str = ast1.ToString();

        // Must be parseable
        var ast2 = BoolExprParser.Parse(str);

        // Must produce same result when parsed again
        var str2 = ast2.ToString();
        Assert.Equal(str, str2);
    }
}
