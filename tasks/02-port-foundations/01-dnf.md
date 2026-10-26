# Dnf

<block id="01-dnf-port">
## TDDAB-1: Port the Dnf Rule Compiler

<intro>
Ports the boolean rule compiler unchanged in behaviour: a text expression over operands `Cx.Fy`
(4 cameras × 16 filters = 64 bits) is parsed into an AST, converted to disjunctive normal form
(each term two `ulong` masks: bits that must be set, bits that must be clear), minimized,
compiled into rules, evaluated against a 64-bit input, edited as a node tree by a UI, and
serialized to the binary format of the embedded board (magic `RL`, version 3).

Capability: exists only in the old code (`../../tmp/zerox/Zerox.Dnf`), no equivalent in .NET.
This block does not redesign it (the operands stay `Cx.Fy`, the masks stay 64-bit).

Source → target (namespace stays `Zerox.Dnf`):

| Old file | New file |
|---|---|
| `../../tmp/zerox/Zerox.Dnf/BoolExpr.cs` | `src/Zerox.Dnf/BoolExpr.cs` |
| `../../tmp/zerox/Zerox.Dnf/BoolExprParser.cs` | `src/Zerox.Dnf/BoolExprParser.cs` |
| `../../tmp/zerox/Zerox.Dnf/DnfConverter.cs` | `src/Zerox.Dnf/DnfConverter.cs` |
| `../../tmp/zerox/Zerox.Dnf/ExpressionNode.cs` | `src/Zerox.Dnf/ExpressionNode.cs` |
| `../../tmp/zerox/Zerox.Dnf/IRuleCompiler.cs` | `src/Zerox.Dnf/IRuleCompiler.cs` |
| `../../tmp/zerox/Zerox.Dnf/OperandsConfig.cs` | `src/Zerox.Dnf/OperandsConfig.cs` |
| `../../tmp/zerox/Zerox.Dnf/RuleCompiler.cs` | `src/Zerox.Dnf/RuleCompiler.cs` |
| `../../tmp/zerox/Zerox.Dnf.Tests/*.cs` (8 files) | `tests/Zerox.Dnf.Tests/` same names |

Changes allowed, nothing else:
- comments and XML docs translated to English; the copyright header lines stay;
- file-scoped namespaces (already), `System.IO` and other unused usings removed (IDE0005);
- `RuleSerializer` constants to PascalCase: `MAGIC_0` → `Magic0`, `MAGIC_1` → `Magic1`, `VERSION` → `Version`, `MAX_RULES` → `MaxRules`, `MAX_TERMS` → `MaxTerms`, `SHOT_LOGIC_INHIBIT` → `ShotLogicInhibit`, `SHOT_LOGIC_SHOOT` → `ShotLogicShoot`, `LINE_BIT` → `LineBit` (no test references them);
- any other analyzer error of the `latest-recommended` set is fixed the smallest way that keeps the behaviour (for example `CultureInfo.InvariantCulture` on `int.Parse`/`ToString`, `ArgumentException` instead of a reserved exception type); the tests stay as they are.

The 8 test files are xunit already (`Assert.*`, `[Fact]`, `[Theory]`, `[InlineData]`); they work on
xunit v3 unchanged except for their usings (`using Xunit;` and `using Zerox.Dnf;` come from the
project: remove the duplicates IDE0005 reports).

The existing `tests/Zerox.Dnf.Tests/AssemblyTests.cs` (from task 01) stays and must stay green:
`Zerox.Dnf` references only the framework.
</intro>

<red>
- test: ParserTests — every test of `../../tmp/zerox/Zerox.Dnf.Tests/ParserTests.cs` passes unchanged
- test: DnfConverterTests — every test of `../../tmp/zerox/Zerox.Dnf.Tests/DnfConverterTests.cs` passes unchanged
- test: EvaluationTests — every test of `../../tmp/zerox/Zerox.Dnf.Tests/EvaluationTests.cs` passes unchanged
- test: ExpressionNodeTests — every test of `../../tmp/zerox/Zerox.Dnf.Tests/ExpressionNodeTests.cs` passes unchanged
- test: OperandsConfigTests — every test of `../../tmp/zerox/Zerox.Dnf.Tests/OperandsConfigTests.cs` passes unchanged
- test: RuleNameTests — every test of `../../tmp/zerox/Zerox.Dnf.Tests/RuleNameTests.cs` passes unchanged
- test: SerializerTests — every test of `../../tmp/zerox/Zerox.Dnf.Tests/SerializerTests.cs` passes unchanged
- test: SerializerV3LineTests — every test of `../../tmp/zerox/Zerox.Dnf.Tests/SerializerV3LineTests.cs` passes unchanged
</red>

### Implementation

Copy, then compile and fix only what the analyzers report. Example of the constant rename in
`src/Zerox.Dnf/RuleCompiler.cs`:

```csharp
public class RuleSerializer : IRuleSerializer
{
    public const byte Magic0 = (byte)'R';
    public const byte Magic1 = (byte)'L';
    public const byte Version = 3;
    public const int MaxRules = 32;
    public const int MaxTerms = 16;

    public const int ShotLogicInhibit = 0;
    public const int ShotLogicShoot = 1;

    // flagsAndCount bit layout: bits 0-3 shot logic, bits 4-8 number of terms (≤ 16), bit 9 shoot line.
    public const int LineBit = 9;   // 0x0200: 0 = lower line (boards 1/2), 1 = upper line (boards 3/4)
    ...   // bodies unchanged, using the new names
}
```

Translation example (`DnfConverter.cs`):

```csharp
/// <summary>An AND term of the DNF: (bit_i &amp; bit_j &amp; ... &amp; !bit_k &amp; !bit_l &amp; ...)</summary>
public readonly struct DnfTerm : IEquatable<DnfTerm>
{
    /// <summary>Bits that MUST be 1</summary>
    public readonly ulong MustBeSet;

    /// <summary>Bits that MUST be 0</summary>
    public readonly ulong MustBeClear;
    ...
}
```

<success>
- [ ] The 8 test files are in `tests/Zerox.Dnf.Tests` and all their tests pass (`dotnet test --project tests/Zerox.Dnf.Tests`), plus the two `AssemblyTests`
- [ ] `grep -rn -E "[àèéìòù]| della | regole | termine " src/Zerox.Dnf tests/Zerox.Dnf.Tests` is empty (no Italian left)
- [ ] `grep -rn -E "MAGIC_|SHOT_LOGIC_|LINE_BIT|MAX_RULES|MAX_TERMS" src tests` is empty
- [ ] `dotnet build zerox.csharp.core.slnx -warnaserror` has 0 warnings
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
