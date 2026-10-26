// ============================================================================
// Zerox.Dnf - Boolean Rule Compiler for DNF expressions
//
// Copyright (c) 2025 3U Vision s.r.l. / Ladislav Sopko
// All rights reserved.
// ============================================================================

using System.Buffers.Binary;

namespace Zerox.Dnf;

/// <summary>
/// Compiled rule ready for the embedded board
/// </summary>
public class CompiledRule
{
    public const int MaxTerms = 16;

    public DnfTerm[] Terms { get; }
    public int NumTerms => Terms.Length;
    public string SourceExpression { get; }

    // v2 metadata
    public short AgeAdjust { get; set; }
    public bool IsShootRule { get; set; } = true;  // default = SHOOT

    // v3 metadata: per-rule shoot line routing.
    // 0 = Lower line (boards 1/2), 1 = Upper line (boards 3/4).
    public int ShootLine { get; set; }

    // Technician-facing rule name (not serialized to HW; used for UI/diagnostics).
    public string Name { get; set; } = "Rule";

    public CompiledRule(string expression, DnfTerm[] terms)
    {
        if (terms.Length > MaxTerms)
            throw new ArgumentException($"Too many terms: {terms.Length} > {MaxTerms}");

        SourceExpression = expression;
        Terms = terms;
    }

    /// <summary>Evaluates the rule against a packed input</summary>
    public bool Evaluate(ulong input)
    {
        for (int i = 0; i < Terms.Length; i++)
        {
            if (Terms[i].Evaluate(input))
                return true;
        }
        return false;
    }

    /// <summary>Evaluates the rule against filterMask[4]</summary>
    public bool Evaluate(ushort[] filterMask)
    {
        if (filterMask.Length != 4)
            throw new ArgumentException("filterMask must have 4 elements");

        ulong input = PackFilterMasks(filterMask);
        return Evaluate(input);
    }

    public static ulong PackFilterMasks(ushort[] fm)
    {
        return (ulong)fm[0]
             | ((ulong)fm[1] << 16)
             | ((ulong)fm[2] << 32)
             | ((ulong)fm[3] << 48);
    }
}

/// <summary>
/// Main compiler: string/AST/node → CompiledRule
/// Thread-safe, can be a singleton
/// </summary>
public class RuleCompiler : IRuleCompiler
{
    /// <summary>
    /// Compiles from a string (for tests)
    /// </summary>
    public CompiledRule Compile(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("Expression cannot be empty");

        var ast = BoolExprParser.Parse(expression);
        return Compile(ast, expression);
    }

    /// <summary>
    /// Compiles from a BoolExpr AST (direct, no parsing)
    /// </summary>
    public CompiledRule Compile(BoolExpr ast, string? sourceExpression = null)
    {
        var dnf = DnfConverter.Convert(ast);
        var expr = sourceExpression ?? ast.ToString();

        if (dnf.IsFalse)
            return new CompiledRule(expr, Array.Empty<DnfTerm>());

        if (dnf.IsTrue)
            return new CompiledRule(expr, new[] { DnfTerm.True });

        if (dnf.Terms.Count > CompiledRule.MaxTerms)
            throw new InvalidOperationException(
                $"Expression expands to {dnf.Terms.Count} terms, max is {CompiledRule.MaxTerms}. " +
                "Consider simplifying the expression.");

        return new CompiledRule(expr, dnf.Terms.ToArray());
    }

    /// <summary>
    /// Compiles from an ExpressionNode (for the UI)
    /// </summary>
    public CompiledRule Compile(ExpressionNode node)
    {
        return Compile(node.ToAst());
    }

    /// <summary>
    /// Compiles several rules from strings
    /// </summary>
    public CompiledRule[] CompileAll(IEnumerable<string> expressions)
    {
        return expressions.Select(Compile).ToArray();
    }

    /// <summary>
    /// Compiles several rules from nodes (for the UI)
    /// </summary>
    public CompiledRule[] CompileAll(IEnumerable<ExpressionNode> nodes)
    {
        return nodes.Select(Compile).ToArray();
    }
}

/// <summary>
/// Serializer of the embedded format
///
/// Binary format (ageAdjust + shotLogic):
///
/// HEADER (4 bytes):
///   magic[2]   : u8[2] = 'R', 'L'  (RuLes)
///   version    : u8    = 3
///   numRules   : u8    = N
///
/// RULE[0..N-1]:
///   ageAdjust      : s16 (little-endian)
///   flagsAndCount  : u16 (little-endian)
///     bits 0-3:  shotLogic (0=INHIBIT, 1=SHOOT)
///     bits 4-8:  numTerms (≤ 16)
///     bit 9:     shoot line (0 = lower, 1 = upper)
///   TERM[0..numTerms-1]:
///     mustBeSet    : u64 (little-endian)
///     mustBeClear  : u64 (little-endian)
/// </summary>
public class RuleSerializer : IRuleSerializer
{
    public const byte Magic0 = (byte)'R';
    public const byte Magic1 = (byte)'L';
    public const byte Version = 3;
    public const int MaxRules = 32;
    public const int MaxTerms = 16;

    public const int ShotLogicInhibit = 0;
    public const int ShotLogicShoot = 1;

    // flagsAndCount bit layout: bits 0-3 shotLogic, bits 4-8 numTerms (≤16), bit 9 shoot line.
    public const int LineBit = 9;   // 0x0200 : 0 = Lower line (boards 1/2), 1 = Upper line (boards 3/4)

    /// <summary>
    /// Pack shotLogic (bits 0-3), numTerms (bits 4-8) and shoot line (bit 9) into u16.
    /// </summary>
    public static ushort PackFlagsAndCount(int shotLogic, int numTerms, int line)
        => (ushort)(((line & 1) << LineBit) | ((numTerms & 0x1F) << 4) | (shotLogic & 0x0F));

    /// <summary>
    /// Convenience overload for line-less callers (line defaults to 0 = Lower).
    /// </summary>
    public static ushort PackFlagsAndCount(int shotLogic, int numTerms)
        => PackFlagsAndCount(shotLogic, numTerms, 0);

    /// <summary>
    /// Unpack shotLogic (bits 0-3), numTerms (bits 4-8) and shoot line (bit 9) from packed u16.
    /// </summary>
    public static (int shotLogic, int numTerms, int line) UnpackFlagsAndCount(ushort packed)
        => (packed & 0x0F, (packed >> 4) & 0x1F, (packed >> LineBit) & 1);

    /// <summary>
    /// Serializes the rules to send them to the embedded board
    /// </summary>
    public byte[] Serialize(CompiledRule[] rules)
    {
        if (rules.Length > MaxRules)
            throw new ArgumentException($"Max {MaxRules} rules, got {rules.Length}");

        // Compute the size
        int size = 4; // header
        foreach (var rule in rules)
        {
            if (rule.NumTerms > MaxTerms)
                throw new ArgumentException($"Rule has {rule.NumTerms} terms, max is {MaxTerms}");
            size += 4; // ageAdjust (2) + flagsAndCount (2)
            size += rule.NumTerms * 16; // 8+8 bytes per term
        }

        var buffer = new byte[size];
        int pos = 0;

        // Header
        buffer[pos++] = Magic0;
        buffer[pos++] = Magic1;
        buffer[pos++] = Version;
        buffer[pos++] = (byte)rules.Length;

        // Rules
        foreach (var rule in rules)
        {
            // ageAdjust (s16)
            BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(pos), rule.AgeAdjust);
            pos += 2;

            // flagsAndCount (u16)
            int shotLogic = rule.IsShootRule ? ShotLogicShoot : ShotLogicInhibit;
            ushort flagsAndCount = PackFlagsAndCount(shotLogic, rule.NumTerms, rule.ShootLine);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(pos), flagsAndCount);
            pos += 2;

            // Terms
            foreach (var term in rule.Terms)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(pos), term.MustBeSet);
                pos += 8;
                BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(pos), term.MustBeClear);
                pos += 8;
            }
        }

        return buffer;
    }

    /// <summary>
    /// Deserializes from the binary format (for tests/debug and round-trip checks)
    /// </summary>
    public CompiledRule[] Deserialize(byte[] data)
    {
        int pos = 0;

        // Validate header
        if (data.Length < 4)
            throw new InvalidOperationException("Data too short for header");

        if (data[pos++] != Magic0 || data[pos++] != Magic1)
            throw new InvalidOperationException("Invalid magic bytes, expected 'RL'");

        byte version = data[pos++];
        if (version != Version)
            throw new InvalidOperationException($"Unsupported version {version}, expected {Version}");

        int numRules = data[pos++];
        var rules = new CompiledRule[numRules];

        for (int r = 0; r < numRules; r++)
        {
            // ageAdjust (s16)
            short ageAdjust = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(pos));
            pos += 2;

            // flagsAndCount (u16)
            ushort flagsAndCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos));
            pos += 2;

            var (shotLogic, numTerms, line) = UnpackFlagsAndCount(flagsAndCount);

            var terms = new DnfTerm[numTerms];

            for (int t = 0; t < numTerms; t++)
            {
                ulong mustBeSet = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(pos));
                pos += 8;
                ulong mustBeClear = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(pos));
                pos += 8;

                terms[t] = new DnfTerm(mustBeSet, mustBeClear);
            }

            rules[r] = new CompiledRule($"rule_{r}", terms)
            {
                AgeAdjust = ageAdjust,
                IsShootRule = shotLogic == ShotLogicShoot,
                ShootLine = line
            };
        }

        return rules;
    }
}
