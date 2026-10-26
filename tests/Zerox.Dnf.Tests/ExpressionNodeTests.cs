
namespace Zerox.Dnf.Tests;

public class ExpressionNodeTests
{
    [Fact]
    public void FromAst_Constant_CreatesCorrectNode()
    {
        var ast = new ConstExpr(true);

        var node = ExpressionNode.FromAst(ast);

        Assert.Equal(NodeType.Constant, node.Type);
        Assert.True(node.ConstValue);
    }

    [Fact]
    public void FromAst_Operand_CreatesCorrectNode()
    {
        var ast = new BitRefExpr(1, 0);  // C1.F0

        var node = ExpressionNode.FromAst(ast);

        Assert.Equal(NodeType.Operand, node.Type);
        Assert.Equal("C1.F0", node.OperandName);
    }

    [Fact]
    public void FromAst_Not_CreatesCorrectNode()
    {
        var ast = new NotExpr(new BitRefExpr(2, 5));  // !C2.F5

        var node = ExpressionNode.FromAst(ast);

        Assert.Equal(NodeType.UnaryOp, node.Type);
        Assert.Equal(BoolOperator.Not, node.Operator);
        Assert.Single(node.Children);
        Assert.Equal("C2.F5", node.Children[0].OperandName);
    }

    [Fact]
    public void FromAst_And_CreatesCorrectNode()
    {
        var ast = new AndExpr(new BitRefExpr(1, 0), new BitRefExpr(2, 1));

        var node = ExpressionNode.FromAst(ast);

        Assert.Equal(NodeType.BinaryOp, node.Type);
        Assert.Equal(BoolOperator.And, node.Operator);
        Assert.Equal(2, node.Children.Count);
    }

    [Fact]
    public void FromAst_Or_CreatesCorrectNode()
    {
        var ast = new OrExpr(new BitRefExpr(3, 10), new BitRefExpr(4, 15));

        var node = ExpressionNode.FromAst(ast);

        Assert.Equal(NodeType.BinaryOp, node.Type);
        Assert.Equal(BoolOperator.Or, node.Operator);
        Assert.Equal(2, node.Children.Count);
    }

    [Fact]
    public void ToAst_Constant_RoundTrip()
    {
        var original = new ConstExpr(false);

        var node = ExpressionNode.FromAst(original);
        var restored = node.ToAst();

        Assert.IsType<ConstExpr>(restored);
        Assert.False(((ConstExpr)restored).Value);
    }

    [Fact]
    public void ToAst_Operand_RoundTrip()
    {
        var original = new BitRefExpr(3, 12);  // C3.F12

        var node = ExpressionNode.FromAst(original);
        var restored = node.ToAst();

        Assert.IsType<BitRefExpr>(restored);
        var bitRef = (BitRefExpr)restored;
        Assert.Equal(3, bitRef.Camera);
        Assert.Equal(12, bitRef.Filter);
    }

    [Fact]
    public void ToAst_ComplexExpression_RoundTrip()
    {
        // (C1.F0 & C2.F1) | !C3.F2
        var original = new OrExpr(
            new AndExpr(new BitRefExpr(1, 0), new BitRefExpr(2, 1)),
            new NotExpr(new BitRefExpr(3, 2))
        );

        var node = ExpressionNode.FromAst(original);
        var restored = node.ToAst();

        Assert.IsType<OrExpr>(restored);
        var orExpr = (OrExpr)restored;
        Assert.IsType<AndExpr>(orExpr.Left);
        Assert.IsType<NotExpr>(orExpr.Right);
    }

    [Fact]
    public void FactoryMethods_CreateCorrectNodes()
    {
        var constNode = ExpressionNode.Constant(true);
        Assert.Equal(NodeType.Constant, constNode.Type);
        Assert.True(constNode.ConstValue);

        var operandNode = ExpressionNode.Operand("C4.F15");
        Assert.Equal(NodeType.Operand, operandNode.Type);
        Assert.Equal("C4.F15", operandNode.OperandName);

        var notNode = ExpressionNode.Not(operandNode);
        Assert.Equal(NodeType.UnaryOp, notNode.Type);
        Assert.Equal(BoolOperator.Not, notNode.Operator);

        var andNode = ExpressionNode.And(
            ExpressionNode.Operand("C1.F0"),
            ExpressionNode.Operand("C2.F1")
        );
        Assert.Equal(NodeType.BinaryOp, andNode.Type);
        Assert.Equal(BoolOperator.And, andNode.Operator);

        var orNode = ExpressionNode.Or(
            ExpressionNode.Operand("C3.F0"),
            ExpressionNode.Operand("C4.F0")
        );
        Assert.Equal(NodeType.BinaryOp, orNode.Type);
        Assert.Equal(BoolOperator.Or, orNode.Operator);
    }

    // ========== NEW: Parse and ToString shortcut tests ==========

    [Theory]
    [InlineData("C1.F0")]
    [InlineData("C1.F0 & C2.F1")]
    [InlineData("C1.F0 | C2.F1")]
    [InlineData("!C1.F0")]
    [InlineData("C1.F0 & (C2.F1 | C2.F2)")]
    [InlineData("(C1.F0 | C1.F1) & (C2.F0 | C2.F1)")]
    public void Parse_ValidExpression_CreatesCorrectTree(string expr)
    {
        var node = ExpressionNode.Parse(expr);

        Assert.NotNull(node);
    }

    [Theory]
    [InlineData("C1.F0", NodeType.Operand)]
    [InlineData("1", NodeType.Constant)]
    [InlineData("0", NodeType.Constant)]
    [InlineData("!C1.F0", NodeType.UnaryOp)]
    [InlineData("C1.F0 & C2.F1", NodeType.BinaryOp)]
    [InlineData("C1.F0 | C2.F1", NodeType.BinaryOp)]
    public void Parse_ReturnsCorrectNodeType(string expr, NodeType expectedType)
    {
        var node = ExpressionNode.Parse(expr);

        Assert.Equal(expectedType, node.Type);
    }

    [Fact]
    public void Parse_And_HasCorrectOperator()
    {
        var node = ExpressionNode.Parse("C1.F0 & C2.F1");

        Assert.Equal(BoolOperator.And, node.Operator);
        Assert.Equal(2, node.Children.Count);
    }

    [Fact]
    public void Parse_Or_HasCorrectOperator()
    {
        var node = ExpressionNode.Parse("C1.F0 | C2.F1");

        Assert.Equal(BoolOperator.Or, node.Operator);
        Assert.Equal(2, node.Children.Count);
    }

    [Theory]
    [InlineData("C1.F0")]
    [InlineData("C1.F0 & C2.F1")]
    [InlineData("C1.F0 | C2.F1")]
    [InlineData("!C1.F0")]
    [InlineData("C1.F0 & (C2.F1 | C2.F2)")]
    public void ToString_Roundtrip_ParseableOutput(string expr)
    {
        // Parse → ToString → Parse again should give equivalent tree
        var node1 = ExpressionNode.Parse(expr);
        var str = node1.ToString();
        var node2 = ExpressionNode.Parse(str);
        var str2 = node2.ToString();

        // String output should stabilize after first roundtrip
        Assert.Equal(str, str2);
    }

    [Fact]
    public void ToString_Operand_ReturnsOperandName()
    {
        var node = ExpressionNode.Operand("C3.F12");

        Assert.Equal("C3.F12", node.ToString());
    }

    [Fact]
    public void ToString_Constant_ReturnsValue()
    {
        Assert.Equal("1", ExpressionNode.Constant(true).ToString());
        Assert.Equal("0", ExpressionNode.Constant(false).ToString());
    }

    [Fact]
    public void ToString_Not_ReturnsWithExclamation()
    {
        var node = ExpressionNode.Not(ExpressionNode.Operand("C1.F0"));

        Assert.Equal("!C1.F0", node.ToString());
    }

    [Fact]
    public void ToString_And_ReturnsWithAmpersand()
    {
        var node = ExpressionNode.And(
            ExpressionNode.Operand("C1.F0"),
            ExpressionNode.Operand("C2.F1")
        );

        Assert.Equal("C1.F0 & C2.F1", node.ToString());
    }

    [Fact]
    public void ToString_Or_ReturnsWithPipe()
    {
        var node = ExpressionNode.Or(
            ExpressionNode.Operand("C1.F0"),
            ExpressionNode.Operand("C2.F1")
        );

        Assert.Equal("C1.F0 | C2.F1", node.ToString());
    }

    [Fact]
    public void Parse_InvalidExpression_Throws()
    {
        Assert.Throws<FormatException>(() => ExpressionNode.Parse("invalid"));
        Assert.Throws<FormatException>(() => ExpressionNode.Parse("C5.F0"));  // Camera 5 invalid
        Assert.Throws<FormatException>(() => ExpressionNode.Parse("C1.F16")); // Filter 16 invalid
    }
}
