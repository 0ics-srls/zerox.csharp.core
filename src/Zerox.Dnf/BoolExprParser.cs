// ============================================================================
// Zerox.Dnf - Boolean Rule Compiler for DNF expressions
//
// Copyright (c) 2025 3U Vision s.r.l. / Ladislav Sopko
// All rights reserved.
// ============================================================================

namespace Zerox.Dnf;

/// <summary>
/// Parser of boolean expressions
///
/// BNF:
/// expr   ::= term ( '|' term )*
/// term   ::= factor ( '&amp;' factor )*
/// factor ::= atom | '(' expr ')' | '!' factor
/// atom   ::= '0' | '1' | bitref
/// bitref ::= 'C' camnum '.' 'F' filtnum
/// camnum ::= '1' | '2' | '3' | '4'
/// filtnum::= '0'..'15'
/// </summary>
public class BoolExprParser
{
    private readonly string _input;
    private int _pos;

    public BoolExprParser(string input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _pos = 0;
    }

    public static BoolExpr Parse(string input)
    {
        var parser = new BoolExprParser(input);
        var result = parser.ParseExpr();
        parser.SkipWhitespace();
        if (parser._pos < parser._input.Length)
        {
            throw new FormatException($"Unexpected character at position {parser._pos}: '{parser._input[parser._pos]}'");
        }
        return result;
    }

    // expr ::= term ( '|' term )*
    private BoolExpr ParseExpr()
    {
        var left = ParseTerm();

        while (TryConsume('|'))
        {
            var right = ParseTerm();
            left = new OrExpr(left, right);
        }

        return left;
    }

    // term ::= factor ( '&' factor )*
    private BoolExpr ParseTerm()
    {
        var left = ParseFactor();

        while (TryConsume('&'))
        {
            var right = ParseFactor();
            left = new AndExpr(left, right);
        }

        return left;
    }

    // factor ::= atom | '(' expr ')' | '!' factor
    private BoolExpr ParseFactor()
    {
        SkipWhitespace();

        if (TryConsume('!') || TryConsume('~'))
        {
            return new NotExpr(ParseFactor());
        }

        if (TryConsume('('))
        {
            var expr = ParseExpr();
            Expect(')');
            return expr;
        }

        return ParseAtom();
    }

    // atom ::= '0' | '1' | bitref
    private BoolExpr ParseAtom()
    {
        SkipWhitespace();

        if (_pos >= _input.Length)
        {
            throw new FormatException("Unexpected end of expression");
        }

        // Constants
        if (TryConsume('0')) return new ConstExpr(false);
        if (TryConsume('1')) return new ConstExpr(true);

        // BitRef: Cx.Fy
        return ParseBitRef();
    }

    // bitref ::= 'C' camnum '.' 'F' filtnum
    private BitRefExpr ParseBitRef()
    {
        Expect('C');
        int camera = ParseInt(1, 4, "Camera number");
        Expect('.');
        Expect('F');
        int filter = ParseInt(0, 15, "Filter number");

        return new BitRefExpr(camera, filter);
    }

    private int ParseInt(int min, int max, string what)
    {
        SkipWhitespace();

        int start = _pos;
        while (_pos < _input.Length && char.IsDigit(_input[_pos]))
        {
            _pos++;
        }

        if (start == _pos)
        {
            throw new FormatException($"Expected {what} at position {_pos}");
        }

        var numStr = _input[start.._pos];
        if (!int.TryParse(numStr, out int value))
        {
            throw new FormatException($"Invalid {what}: '{numStr}'");
        }

        if (value < min || value > max)
        {
            throw new FormatException($"{what} must be between {min} and {max}, got {value}");
        }

        return value;
    }

    private void SkipWhitespace()
    {
        while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
        {
            _pos++;
        }
    }

    private bool TryConsume(char c)
    {
        SkipWhitespace();
        if (_pos < _input.Length && _input[_pos] == c)
        {
            _pos++;
            return true;
        }
        return false;
    }

    private void Expect(char c)
    {
        SkipWhitespace();
        if (_pos >= _input.Length || _input[_pos] != c)
        {
            var found = _pos < _input.Length ? $"'{_input[_pos]}'" : "end of input";
            throw new FormatException($"Expected '{c}' at position {_pos}, found {found}");
        }
        _pos++;
    }
}
