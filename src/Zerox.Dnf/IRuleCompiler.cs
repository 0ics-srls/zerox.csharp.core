// ============================================================================
// Zerox.Dnf - Boolean Rule Compiler for DNF expressions
//
// Copyright (c) 2025 3U Vision s.r.l. / Ladislav Sopko
// All rights reserved.
// ============================================================================

namespace Zerox.Dnf;

/// <summary>
/// Rule compiler; stateless, one instance can serve the whole application
/// </summary>
public interface IRuleCompiler
{
    /// <summary>
    /// Compiles from a string (for tests)
    /// </summary>
    CompiledRule Compile(string expression);

    /// <summary>
    /// Compiles from a BoolExpr AST (direct, no parsing)
    /// </summary>
    CompiledRule Compile(BoolExpr ast, string? sourceExpression = null);

    /// <summary>
    /// Compiles from an ExpressionNode (for the UI)
    /// </summary>
    CompiledRule Compile(ExpressionNode node);

    /// <summary>
    /// Compiles several rules from strings
    /// </summary>
    CompiledRule[] CompileAll(IEnumerable<string> expressions);

    /// <summary>
    /// Compiles several rules from nodes (for the UI)
    /// </summary>
    CompiledRule[] CompileAll(IEnumerable<ExpressionNode> nodes);
}

/// <summary>
/// Rule serializer; stateless
/// v2 format only - no GenerateCHeader
/// </summary>
public interface IRuleSerializer
{
    /// <summary>
    /// Serializes rules in the binary format for the embedded board (C4)
    /// </summary>
    byte[] Serialize(CompiledRule[] rules);

    /// <summary>
    /// Deserializes from the binary format (for tests/debug)
    /// </summary>
    CompiledRule[] Deserialize(byte[] data);
}
