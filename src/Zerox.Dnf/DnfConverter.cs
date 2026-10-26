// ============================================================================
// Zerox.Dnf - Boolean Rule Compiler for DNF expressions
//
// Copyright (c) 2025 3U Vision s.r.l. / Ladislav Sopko
// All rights reserved.
// ============================================================================


namespace Zerox.Dnf;

/// <summary>
/// An AND term of the DNF.
/// Represents: (bit_i &amp; bit_j &amp; ... &amp; !bit_k &amp; !bit_l &amp; ...)
/// </summary>
public readonly struct DnfTerm : IEquatable<DnfTerm>
{
    /// <summary>Bits that MUST be 1</summary>
    public ulong MustBeSet { get; }

    /// <summary>Bits that MUST be 0</summary>
    public ulong MustBeClear { get; }

    public DnfTerm(ulong mustBeSet, ulong mustBeClear)
    {
        MustBeSet = mustBeSet;
        MustBeClear = mustBeClear;
    }

    /// <summary>Always-true term (no requirement)</summary>
    public static DnfTerm True => new(0, 0);

    /// <summary>Contradictory term (impossible)</summary>
    public bool IsContradiction => (MustBeSet & MustBeClear) != 0;

    /// <summary>Always-true term</summary>
    public bool IsTautology => MustBeSet == 0 && MustBeClear == 0;

    /// <summary>AND of two terms</summary>
    public DnfTerm And(DnfTerm other) => new(
        MustBeSet | other.MustBeSet,
        MustBeClear | other.MustBeClear
    );

    /// <summary>Evaluates the term against an input</summary>
    public bool Evaluate(ulong input) =>
        ((input & MustBeSet) == MustBeSet) &&
        ((input & MustBeClear) == 0);

    public bool Equals(DnfTerm other) =>
        MustBeSet == other.MustBeSet && MustBeClear == other.MustBeClear;

    public override bool Equals(object? obj) => obj is DnfTerm t && Equals(t);
    public static bool operator ==(DnfTerm left, DnfTerm right) => left.Equals(right);
    public static bool operator !=(DnfTerm left, DnfTerm right) => !left.Equals(right);
    public override int GetHashCode() => HashCode.Combine(MustBeSet, MustBeClear);

    public override string ToString()
    {
        if (IsTautology) return "true";
        if (IsContradiction) return "false";

        var parts = new List<string>();
        for (int i = 0; i < 64; i++)
        {
            ulong mask = 1UL << i;
            int cam = (i / 16) + 1;
            int filt = i % 16;

            if ((MustBeSet & mask) != 0)
                parts.Add($"C{cam}.F{filt}");
            else if ((MustBeClear & mask) != 0)
                parts.Add($"!C{cam}.F{filt}");
        }
        return string.Join(" & ", parts);
    }
}

/// <summary>
/// Result of the DNF conversion: OR of AND terms
/// </summary>
public class DnfResult : IEquatable<DnfResult>
{
    public List<DnfTerm> Terms { get; }

    /// <summary>Always-false expression (no term)</summary>
    public bool IsFalse => Terms.Count == 0;

    /// <summary>Always-true expression (holds a tautological term)</summary>
    public bool IsTrue => Terms.Any(t => t.IsTautology);

    public DnfResult(IEnumerable<DnfTerm> terms)
    {
        // Remove contradictions and duplicates
        Terms = terms
            .Where(t => !t.IsContradiction)
            .Distinct()
            .ToList();

        //Minimize();

        // With a tautological term the whole expression is true
        if (Terms.Any(t => t.IsTautology))
        {
            Terms.Clear();
            Terms.Add(DnfTerm.True);
        }
    }

    // If called checks for possible warnings
    // used to check in the editor at the moment, think if we need it in the ctor of dnf
    public void Minimize()
    {
        if (Terms.Count <= 1) return;

        bool changed;
        do
        {
            changed = false;

            for (int i = 0; i < Terms.Count; i++)
            {
                for (int j = 0; j < Terms.Count; j++)
                {
                    if (i == j) continue;

                    var t1 = Terms[i];
                    var t2 = Terms[j];

                    // 1. Absorption: is t2 contained in t1? (e.g. A | (A & B) = A)
                    // If t1 is more restrictive than t2, t1 is useless.
                    if ((t1.MustBeSet & t2.MustBeSet) == t2.MustBeSet &&
                        (t1.MustBeClear & t2.MustBeClear) == t2.MustBeClear)
                    {
                        Terms.RemoveAt(i);
                        changed = true; i--; break;
                    }

                    // 2. Complement simplification: can t2 simplify t1?
                    // If t2 is a single literal (e.g. !A) and t1 requires A, A is removed from t1.
                    // Example: !A | (A & B) => !A | B
                    if (TrySimplifyComplement(ref t1, t2))
                    {
                        Terms[i] = t1;
                        changed = true;
                    }

                    // 3. Resolution (adjacency): do t1 and t2 differ in one bit only?
                    // Example: (A & B) | (A & !B) => A
                    if (TryCombine(t1, t2, out var combined))
                    {
                        Terms[i] = combined;
                        Terms.RemoveAt(j);
                        changed = true;
                        if (j < i) i--;
                        break;
                    }
                }
            }
        } while (changed);
    }

    private static bool TrySimplifyComplement(ref DnfTerm target, DnfTerm literal)
    {
        // Works when 'literal' has a single bit (either Set or Clear)
        ulong allBits = literal.MustBeSet | literal.MustBeClear;
        if ((allBits != 0 && (allBits & (allBits - 1)) == 0))
        {
            // If literal is A and target requires !A, remove !A from target
            if (literal.MustBeSet != 0 && (target.MustBeClear & literal.MustBeSet) != 0)
            {
                target = new DnfTerm(target.MustBeSet, target.MustBeClear & ~literal.MustBeSet);
                return true;
            }
            // If literal is !A and target requires A, remove A from target
            if (literal.MustBeClear != 0 && (target.MustBeSet & literal.MustBeClear) != 0)
            {
                target = new DnfTerm(target.MustBeSet & ~literal.MustBeClear, target.MustBeClear);
                return true;
            }
        }
        return false;
    }

    private static bool TryCombine(DnfTerm t1, DnfTerm t2, out DnfTerm combined)
    {
        combined = default;
        ulong diffSet = t1.MustBeSet ^ t2.MustBeSet;
        ulong diffClear = t1.MustBeClear ^ t2.MustBeClear;

        // Classic case: (A & B) | (A & !B) => A
        if (diffSet == diffClear && diffSet != 0 && (diffSet & (diffSet - 1)) == 0)
        {
            combined = new DnfTerm(t1.MustBeSet & ~diffSet, t1.MustBeClear & ~diffClear);
            return true;
        }
        return false;
    }

    public bool Equals(DnfResult? other)
    {
        if (other == null || Terms.Count != other.Terms.Count) return false;
        return Terms.All(t => other.Terms.Contains(t));
    }

    public override bool Equals(object? obj) => obj is DnfResult other && Equals(other);
    public override int GetHashCode()
    {
        int hash = 0;
        foreach (var t in Terms) hash ^= t.GetHashCode();
        return hash;
    }

    /// <summary>Evaluates the DNF against an input</summary>
    public bool Evaluate(ulong input) => Terms.Any(t => t.Evaluate(input));

    public override string ToString()
    {
        if (IsFalse) return "false";
        if (IsTrue) return "true";
        return string.Join(" | ", Terms.Select(t => Terms.Count > 1 ? $"({t})" : t.ToString()));
    }
}

/// <summary>
/// Converter boolean expression → DNF
/// </summary>
public class DnfConverter : IBoolExprVisitor<DnfResult>
{
    public static DnfResult Convert(BoolExpr expr)
    {
        var converter = new DnfConverter();
        return expr.Accept(converter);
    }

    public DnfResult VisitConst(ConstExpr expr)
    {
        if (expr.Value)
            return new DnfResult(new[] { DnfTerm.True });
        else
            return new DnfResult(Array.Empty<DnfTerm>());
    }

    public DnfResult VisitBitRef(BitRefExpr expr)
    {
        var term = new DnfTerm(expr.BitMask, 0);
        return new DnfResult(new[] { term });
    }

    public DnfResult VisitNot(NotExpr expr)
    {
        // Applies De Morgan recursively
        return NegateExpr(expr.Operand);
    }

    public DnfResult VisitAnd(AndExpr expr)
    {
        var left = expr.Left.Accept(this);
        var right = expr.Right.Accept(this);

        // (A|B) & (C|D) = (A&C) | (A&D) | (B&C) | (B&D)
        // Distribution of AND over OR
        var terms = new List<DnfTerm>();
        foreach (var lt in left.Terms)
        {
            foreach (var rt in right.Terms)
            {
                terms.Add(lt.And(rt));
            }
        }
        return new DnfResult(terms);
    }

    public DnfResult VisitOr(OrExpr expr)
    {
        var left = expr.Left.Accept(this);
        var right = expr.Right.Accept(this);

        // Plain union of the terms
        return new DnfResult(left.Terms.Concat(right.Terms));
    }

    /// <summary>
    /// Negates an expression and returns the DNF of the result
    /// Uses De Morgan: !(A&amp;B) = !A | !B, !(A|B) = !A &amp; !B
    /// </summary>
    private DnfResult NegateExpr(BoolExpr expr)
    {
        switch (expr)
        {
            case ConstExpr c:
                return new ConstExpr(!c.Value).Accept(this);

            case BitRefExpr b:
                // !Cx.Fy → term with mustBeClear
                var term = new DnfTerm(0, b.BitMask);
                return new DnfResult(new[] { term });

            case NotExpr n:
                // !!A = A
                return n.Operand.Accept(this);

            case AndExpr a:
                // !(A & B) = !A | !B
                var notLeft = NegateExpr(a.Left);
                var notRight = NegateExpr(a.Right);
                return new DnfResult(notLeft.Terms.Concat(notRight.Terms));

            case OrExpr o:
                // !(A | B) = !A & !B
                var nLeft = NegateExpr(o.Left);
                var nRight = NegateExpr(o.Right);
                // Distributed AND
                var terms = new List<DnfTerm>();
                foreach (var lt in nLeft.Terms)
                {
                    foreach (var rt in nRight.Terms)
                    {
                        terms.Add(lt.And(rt));
                    }
                }
                return new DnfResult(terms);

            default:
                throw new NotSupportedException($"Unknown expression type: {expr.GetType()}");
        }
    }
}
