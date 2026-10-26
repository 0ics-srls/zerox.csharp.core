
namespace Zerox.Dnf.Tests;

public class EvaluationTests
{
    private readonly RuleCompiler _compiler = new();

    private static ulong Pack(ushort c1, ushort c2, ushort c3, ushort c4)
    {
        return (ulong)c1
             | ((ulong)c2 << 16)
             | ((ulong)c3 << 32)
             | ((ulong)c4 << 48);
    }

    [Fact]
    public void Evaluate_SingleBit_MatchesWhenSet()
    {
        var rule = _compiler.Compile("C1.F0");

        Assert.True(rule.Evaluate(Pack(0x0001, 0, 0, 0)));   // C1.F0 = 1
        Assert.False(rule.Evaluate(Pack(0x0000, 0, 0, 0)));  // C1.F0 = 0
        Assert.False(rule.Evaluate(Pack(0x0002, 0, 0, 0)));  // C1.F1 = 1, F0 = 0
    }

    [Fact]
    public void Evaluate_And_RequiresBothBits()
    {
        var rule = _compiler.Compile("C1.F0 & C2.F0");

        Assert.True(rule.Evaluate(Pack(0x0001, 0x0001, 0, 0)));   // both set
        Assert.False(rule.Evaluate(Pack(0x0001, 0x0000, 0, 0)));  // only C1
        Assert.False(rule.Evaluate(Pack(0x0000, 0x0001, 0, 0)));  // only C2
        Assert.False(rule.Evaluate(Pack(0x0000, 0x0000, 0, 0)));  // none
    }

    [Fact]
    public void Evaluate_Or_RequiresAtLeastOne()
    {
        var rule = _compiler.Compile("C1.F0 | C2.F0");

        Assert.True(rule.Evaluate(Pack(0x0001, 0x0001, 0, 0)));   // both
        Assert.True(rule.Evaluate(Pack(0x0001, 0x0000, 0, 0)));   // only C1
        Assert.True(rule.Evaluate(Pack(0x0000, 0x0001, 0, 0)));   // only C2
        Assert.False(rule.Evaluate(Pack(0x0000, 0x0000, 0, 0)));  // none
    }

    [Fact]
    public void Evaluate_Not_MatchesWhenClear()
    {
        var rule = _compiler.Compile("!C1.F0");

        Assert.True(rule.Evaluate(Pack(0x0000, 0, 0, 0)));   // C1.F0 = 0
        Assert.False(rule.Evaluate(Pack(0x0001, 0, 0, 0)));  // C1.F0 = 1
    }

    [Fact]
    public void Evaluate_AndWithNot_C1SetAndC2Clear()
    {
        var rule = _compiler.Compile("C1.F0 & !C2.F0");

        Assert.True(rule.Evaluate(Pack(0x0001, 0x0000, 0, 0)));   // C1=1, C2=0
        Assert.False(rule.Evaluate(Pack(0x0001, 0x0001, 0, 0)));  // C1=1, C2=1
        Assert.False(rule.Evaluate(Pack(0x0000, 0x0000, 0, 0)));  // C1=0, C2=0
        Assert.False(rule.Evaluate(Pack(0x0000, 0x0001, 0, 0)));  // C1=0, C2=1
    }

    [Fact]
    public void Evaluate_Complex_Expression()
    {
        // C1.F1 & (C2.F2 | C2.F3)
        var rule = _compiler.Compile("C1.F1 & (C2.F2 | C2.F3)");

        // C1.F1=1, C2.F2=1 → true
        Assert.True(rule.Evaluate(Pack(0x0002, 0x0004, 0, 0)));

        // C1.F1=1, C2.F3=1 → true
        Assert.True(rule.Evaluate(Pack(0x0002, 0x0008, 0, 0)));

        // C1.F1=1, C2.F2=1, C2.F3=1 → true
        Assert.True(rule.Evaluate(Pack(0x0002, 0x000C, 0, 0)));

        // C1.F1=1, C2=0 → false
        Assert.False(rule.Evaluate(Pack(0x0002, 0x0000, 0, 0)));

        // C1.F1=0, C2.F2=1 → false
        Assert.False(rule.Evaluate(Pack(0x0000, 0x0004, 0, 0)));
    }

    [Fact]
    public void Evaluate_AllCameras()
    {
        var rule = _compiler.Compile("C1.F0 & C2.F0 & C3.F0 & C4.F0");

        Assert.True(rule.Evaluate(Pack(0x0001, 0x0001, 0x0001, 0x0001)));
        Assert.False(rule.Evaluate(Pack(0x0001, 0x0001, 0x0001, 0x0000)));  // C4 missing
        Assert.False(rule.Evaluate(Pack(0x0001, 0x0001, 0x0000, 0x0001)));  // C3 missing
    }

    [Fact]
    public void Evaluate_HighFilters()
    {
        var rule = _compiler.Compile("C1.F15 & C4.F15");

        Assert.True(rule.Evaluate(Pack(0x8000, 0, 0, 0x8000)));   // F15 of C1 and C4
        Assert.False(rule.Evaluate(Pack(0x8000, 0, 0, 0x0000)));  // only C1.F15
    }

    [Fact]
    public void Evaluate_ConstantTrue_AlwaysMatches()
    {
        var rule = _compiler.Compile("1");

        Assert.True(rule.Evaluate(Pack(0, 0, 0, 0)));
        Assert.True(rule.Evaluate(Pack(0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF)));
    }

    [Fact]
    public void Evaluate_ConstantFalse_NeverMatches()
    {
        var rule = _compiler.Compile("0");

        Assert.False(rule.Evaluate(Pack(0, 0, 0, 0)));
        Assert.False(rule.Evaluate(Pack(0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF)));
    }

    [Fact]
    public void Evaluate_UsingFilterMaskArray()
    {
        var rule = _compiler.Compile("C1.F1 & C2.F2");

        Assert.True(rule.Evaluate(new ushort[] { 0x0002, 0x0004, 0, 0 }));
        Assert.False(rule.Evaluate(new ushort[] { 0x0002, 0x0000, 0, 0 }));
    }

    [Fact]
    public void Evaluate_RealWorldCase_RgbAndSwir()
    {
        // RGB (C1) sees filter 5 (brown), SWIR (C3) sees filter 2 (black)
        var rule = _compiler.Compile("C1.F5 & C3.F2");

        // Chip with a stain → shoot
        Assert.True(rule.Evaluate(Pack(0x0020, 0, 0x0004, 0)));

        // Only brown without a stain → no shoot
        Assert.False(rule.Evaluate(Pack(0x0020, 0, 0x0000, 0)));

        // Only a stain without brown → no shoot
        Assert.False(rule.Evaluate(Pack(0x0000, 0, 0x0004, 0)));
    }

    // =========================================================================
    // Tests for Compile(BoolExpr) and Compile(ExpressionNode) overloads
    // =========================================================================

    [Fact]
    public void Compile_FromBoolExpr_ProducesEquivalentRule()
    {
        // Arrange: build the AST by hand
        var ast = new AndExpr(
            new BitRefExpr(1, 0),  // C1.F0
            new BitRefExpr(2, 1)   // C2.F1
        );

        // Act
        var rule = _compiler.Compile(ast);

        // Assert: same behaviour as the string
        var ruleFromString = _compiler.Compile("C1.F0 & C2.F1");
        Assert.True(rule.Evaluate(Pack(0x0001, 0x0002, 0, 0)));
        Assert.False(rule.Evaluate(Pack(0x0001, 0x0000, 0, 0)));
        Assert.Equal(ruleFromString.NumTerms, rule.NumTerms);
    }

    [Fact]
    public void Compile_FromBoolExpr_WithNot()
    {
        // !C1.F0
        var ast = new NotExpr(new BitRefExpr(1, 0));

        var rule = _compiler.Compile(ast);

        Assert.True(rule.Evaluate(Pack(0x0000, 0, 0, 0)));   // C1.F0 = 0
        Assert.False(rule.Evaluate(Pack(0x0001, 0, 0, 0)));  // C1.F0 = 1
    }

    [Fact]
    public void Compile_FromBoolExpr_ComplexOr()
    {
        // C1.F0 | C2.F0
        var ast = new OrExpr(
            new BitRefExpr(1, 0),
            new BitRefExpr(2, 0)
        );

        var rule = _compiler.Compile(ast);

        Assert.True(rule.Evaluate(Pack(0x0001, 0x0000, 0, 0)));
        Assert.True(rule.Evaluate(Pack(0x0000, 0x0001, 0, 0)));
        Assert.True(rule.Evaluate(Pack(0x0001, 0x0001, 0, 0)));
        Assert.False(rule.Evaluate(Pack(0x0000, 0x0000, 0, 0)));
    }

    [Fact]
    public void Compile_FromExpressionNode_SimpleOperand()
    {
        // Arrange
        var node = ExpressionNode.Operand("C1.F0");

        // Act
        var rule = _compiler.Compile(node);

        // Assert
        Assert.True(rule.Evaluate(Pack(0x0001, 0, 0, 0)));
        Assert.False(rule.Evaluate(Pack(0x0000, 0, 0, 0)));
    }

    [Fact]
    public void Compile_FromExpressionNode_AndExpression()
    {
        // C1.F0 & C2.F1
        var node = ExpressionNode.And(
            ExpressionNode.Operand("C1.F0"),
            ExpressionNode.Operand("C2.F1")
        );

        var rule = _compiler.Compile(node);

        Assert.True(rule.Evaluate(Pack(0x0001, 0x0002, 0, 0)));
        Assert.False(rule.Evaluate(Pack(0x0001, 0x0000, 0, 0)));
    }

    [Fact]
    public void Compile_FromExpressionNode_ComplexTree()
    {
        // C1.F1 & (C2.F2 | C2.F3)
        var node = ExpressionNode.And(
            ExpressionNode.Operand("C1.F1"),
            ExpressionNode.Or(
                ExpressionNode.Operand("C2.F2"),
                ExpressionNode.Operand("C2.F3")
            )
        );

        var rule = _compiler.Compile(node);

        // Check the equivalence with the string
        var ruleFromString = _compiler.Compile("C1.F1 & (C2.F2 | C2.F3)");

        Assert.True(rule.Evaluate(Pack(0x0002, 0x0004, 0, 0)));   // C1.F1 & C2.F2
        Assert.True(rule.Evaluate(Pack(0x0002, 0x0008, 0, 0)));   // C1.F1 & C2.F3
        Assert.False(rule.Evaluate(Pack(0x0002, 0x0000, 0, 0)));  // only C1.F1
        Assert.Equal(ruleFromString.NumTerms, rule.NumTerms);
    }

    [Fact]
    public void Compile_FromExpressionNode_WithNot()
    {
        // C1.F0 & !C2.F0
        var node = ExpressionNode.And(
            ExpressionNode.Operand("C1.F0"),
            ExpressionNode.Not(ExpressionNode.Operand("C2.F0"))
        );

        var rule = _compiler.Compile(node);

        Assert.True(rule.Evaluate(Pack(0x0001, 0x0000, 0, 0)));   // C1=1, C2=0
        Assert.False(rule.Evaluate(Pack(0x0001, 0x0001, 0, 0)));  // C1=1, C2=1
    }

    [Fact]
    public void CompileAll_FromExpressionNodes_CompilesAll()
    {
        var nodes = new[]
        {
            ExpressionNode.Operand("C1.F0"),
            ExpressionNode.And(
                ExpressionNode.Operand("C2.F1"),
                ExpressionNode.Operand("C3.F2")
            )
        };

        var rules = _compiler.CompileAll(nodes);

        Assert.Equal(2, rules.Length);
        Assert.True(rules[0].Evaluate(Pack(0x0001, 0, 0, 0)));
        Assert.True(rules[1].Evaluate(Pack(0, 0x0002, 0x0004, 0)));
    }

    [Fact]
    public void Compile_FromExpressionNode_EquivalentToString()
    {
        // Same result from the string and from the node
        var node = ExpressionNode.And(
            ExpressionNode.Operand("C1.F5"),
            ExpressionNode.Or(
                ExpressionNode.Operand("C2.F2"),
                ExpressionNode.Not(ExpressionNode.Operand("C3.F0"))
            )
        );

        var ruleFromNode = _compiler.Compile(node);
        var ruleFromString = _compiler.Compile("C1.F5 & (C2.F2 | !C3.F0)");

        // Test on several inputs
        var testInputs = new[]
        {
            Pack(0x0020, 0x0004, 0x0000, 0),  // C1.F5=1, C2.F2=1, C3.F0=0
            Pack(0x0020, 0x0000, 0x0000, 0),  // C1.F5=1, !C3.F0
            Pack(0x0020, 0x0004, 0x0001, 0),  // C1.F5=1, C2.F2=1, C3.F0=1
            Pack(0x0000, 0x0004, 0x0000, 0),  // C1.F5=0
        };

        foreach (var input in testInputs)
        {
            Assert.Equal(ruleFromString.Evaluate(input), ruleFromNode.Evaluate(input));
        }
    }
}
