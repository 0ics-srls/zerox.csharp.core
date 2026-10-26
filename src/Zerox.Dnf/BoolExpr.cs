// ============================================================================
// Zerox.Dnf - Boolean Rule Compiler for DNF expressions
//
// Copyright (c) 2025 3U Vision s.r.l. / Ladislav Sopko
// All rights reserved.
// ============================================================================

namespace Zerox.Dnf;

/// <summary>
/// AST nodes of boolean expressions
/// </summary>
public abstract record BoolExpr
{
    public abstract T Accept<T>(IBoolExprVisitor<T> visitor);
}

public record ConstExpr(bool Value) : BoolExpr
{
    public override T Accept<T>(IBoolExprVisitor<T> visitor) => visitor.VisitConst(this);
    public override string ToString() => Value ? "1" : "0";
}

public record BitRefExpr(int Camera, int Filter) : BoolExpr
{
    // Camera: 1-4, Filter: 0-15
    // BitIndex = (Camera - 1) * 16 + Filter
    public int BitIndex => (Camera - 1) * 16 + Filter;
    public ulong BitMask => 1UL << BitIndex;

    public override T Accept<T>(IBoolExprVisitor<T> visitor) => visitor.VisitBitRef(this);
    public override string ToString() => $"C{Camera}.F{Filter}";
}

public record NotExpr(BoolExpr Operand) : BoolExpr
{
    public override T Accept<T>(IBoolExprVisitor<T> visitor) => visitor.VisitNot(this);
    public override string ToString() => Operand switch
    {
        BitRefExpr or ConstExpr or NotExpr => $"!{Operand}",
        _ => $"!({Operand})"  // Wrap binary ops in parens
    };
}

public record AndExpr(BoolExpr Left, BoolExpr Right) : BoolExpr
{
    public override T Accept<T>(IBoolExprVisitor<T> visitor) => visitor.VisitAnd(this);
    public override string ToString()
    {
        // AND has higher precedence than OR, so wrap OR children in parens
        var left = Left is OrExpr ? $"({Left})" : Left.ToString();
        var right = Right is OrExpr ? $"({Right})" : Right.ToString();
        return $"{left} & {right}";
    }
}

public record OrExpr(BoolExpr Left, BoolExpr Right) : BoolExpr
{
    public override T Accept<T>(IBoolExprVisitor<T> visitor) => visitor.VisitOr(this);
    public override string ToString()
    {
        // OR has lower precedence than AND, no parens needed for AND children
        return $"{Left} | {Right}";
    }
}

public interface IBoolExprVisitor<T>
{
    T VisitConst(ConstExpr expr);
    T VisitBitRef(BitRefExpr expr);
    T VisitNot(NotExpr expr);
    T VisitAnd(AndExpr expr);
    T VisitOr(OrExpr expr);
}
