// ============================================================================
// Zerox.Dnf - ExpressionNode - POCO for UI node tree representation
//
// Copyright (c) 2025 3U Vision s.r.l. / Ladislav Sopko
// All rights reserved.
// ============================================================================

namespace Zerox.Dnf;

/// <summary>
/// Node type in expression tree
/// </summary>
public enum NodeType
{
    /// <summary>Boolean constant (true/false)</summary>
    Constant,

    /// <summary>Operand reference (e.g., "C1.F0")</summary>
    Operand,

    /// <summary>Unary operator (NOT) - 1 child</summary>
    UnaryOp,

    /// <summary>Binary operator (AND, OR) - 2+ children</summary>
    BinaryOp
}

/// <summary>
/// Boolean operator type
/// </summary>
public enum BoolOperator
{
    None,
    Not,
    And,
    Or
}

/// <summary>
/// POCO node for UI expression tree representation.
/// No INotifyPropertyChanged - just plain data.
/// UI takes data, modifies it, returns it.
/// </summary>
public class ExpressionNode
{
    /// <summary>Type of this node</summary>
    public NodeType Type { get; set; }

    /// <summary>Operator (for UnaryOp and BinaryOp types)</summary>
    public BoolOperator Operator { get; set; }

    /// <summary>Child nodes (for UnaryOp: 1 child, for BinaryOp: 2+ children)</summary>
    public List<ExpressionNode> Children { get; set; } = new();

    /// <summary>Operand name (for Operand type, e.g., "C1.F0")</summary>
    public string? OperandName { get; set; }

    /// <summary>Constant value (for Constant type)</summary>
    public bool? ConstValue { get; set; }

    /// <summary>
    /// Create node from AST BoolExpr
    /// </summary>
    public static ExpressionNode FromAst(BoolExpr expr)
    {
        return expr switch
        {
            ConstExpr c => new ExpressionNode
            {
                Type = NodeType.Constant,
                ConstValue = c.Value
            },
            BitRefExpr b => new ExpressionNode
            {
                Type = NodeType.Operand,
                OperandName = b.ToString()
            },
            NotExpr n => new ExpressionNode
            {
                Type = NodeType.UnaryOp,
                Operator = BoolOperator.Not,
                Children = new List<ExpressionNode> { FromAst(n.Operand) }
            },
            AndExpr a => new ExpressionNode
            {
                Type = NodeType.BinaryOp,
                Operator = BoolOperator.And,
                Children = new List<ExpressionNode> { FromAst(a.Left), FromAst(a.Right) }
            },
            OrExpr o => new ExpressionNode
            {
                Type = NodeType.BinaryOp,
                Operator = BoolOperator.Or,
                Children = new List<ExpressionNode> { FromAst(o.Left), FromAst(o.Right) }
            },
            _ => throw new ArgumentException($"Unknown expression type: {expr.GetType()}")
        };
    }

    /// <summary>
    /// Convert this node tree back to AST BoolExpr
    /// </summary>
    public BoolExpr ToAst()
    {
        return Type switch
        {
            NodeType.Constant => new ConstExpr(ConstValue ?? false),
            NodeType.Operand => ParseOperand(OperandName ?? "C1.F0"),
            NodeType.UnaryOp when Operator == BoolOperator.Not
                => new NotExpr(Children[0].ToAst()),
            NodeType.BinaryOp when Operator == BoolOperator.And
                => BuildBinaryTree(Children, (l, r) => new AndExpr(l, r)),
            NodeType.BinaryOp when Operator == BoolOperator.Or
                => BuildBinaryTree(Children, (l, r) => new OrExpr(l, r)),
            _ => throw new InvalidOperationException($"Invalid node: Type={Type}, Operator={Operator}")
        };
    }

    private static BitRefExpr ParseOperand(string name)
    {
        if (OperandsConfig.TryParseBitRef(name, out int cam, out int filt))
        {
            return new BitRefExpr(cam, filt);
        }
        throw new ArgumentException($"Invalid operand: {name}");
    }

    private static BoolExpr BuildBinaryTree(List<ExpressionNode> children, Func<BoolExpr, BoolExpr, BoolExpr> combine)
    {
        if (children.Count == 0)
            throw new InvalidOperationException("Binary operator requires at least one child");

        if (children.Count == 1)
            return children[0].ToAst();

        var result = children[0].ToAst();
        for (int i = 1; i < children.Count; i++)
        {
            result = combine(result, children[i].ToAst());
        }
        return result;
    }

    /// <summary>
    /// Create a constant node
    /// </summary>
    public static ExpressionNode Constant(bool value) => new()
    {
        Type = NodeType.Constant,
        ConstValue = value
    };

    /// <summary>
    /// Create an operand node
    /// </summary>
    public static ExpressionNode Operand(string name) => new()
    {
        Type = NodeType.Operand,
        OperandName = name
    };

    /// <summary>
    /// Create a NOT node
    /// </summary>
    public static ExpressionNode Not(ExpressionNode child) => new()
    {
        Type = NodeType.UnaryOp,
        Operator = BoolOperator.Not,
        Children = new List<ExpressionNode> { child }
    };

    /// <summary>
    /// Create an AND node
    /// </summary>
    public static ExpressionNode And(params ExpressionNode[] children) => new()
    {
        Type = NodeType.BinaryOp,
        Operator = BoolOperator.And,
        Children = new List<ExpressionNode>(children)
    };

    /// <summary>
    /// Create an OR node
    /// </summary>
    public static ExpressionNode Or(params ExpressionNode[] children) => new()
    {
        Type = NodeType.BinaryOp,
        Operator = BoolOperator.Or,
        Children = new List<ExpressionNode>(children)
    };

    // ========== Shortcut methods for String ↔ NodeTree ==========

    /// <summary>
    /// Parse expression string directly to node tree.
    /// Shortcut for FromAst(BoolExprParser.Parse(expr))
    /// </summary>
    public static ExpressionNode Parse(string expression)
        => FromAst(BoolExprParser.Parse(expression));

    /// <summary>
    /// Convert node tree to expression string.
    /// Shortcut for ToAst().ToString()
    /// </summary>
    public override string ToString()
        => ToAst().ToString();
}
