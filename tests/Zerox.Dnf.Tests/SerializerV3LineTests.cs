
namespace Zerox.Dnf.Tests;

/// <summary>
/// TDDAB block 01 — per-rule shoot line (ShootLine) + v3 wire format.
/// Line rides flagsAndCount bit 9 (0x0200): 0 = Lower line (boards 1/2), 1 = Upper line (boards 3/4).
/// </summary>
public class SerializerV3LineTests
{
    private readonly RuleCompiler _compiler = new();
    private readonly RuleSerializer _serializer = new();

    /*--- Pack/Unpack with line ---*/

    [Fact]
    public void PackFlagsAndCount_WithLine_SetsBit9_AndPreservesShotLogicAndNumTerms()
    {
        // shotLogic=1 (bits 0-3), numTerms=16 (0x10 → bits 4-8), line=1 (bit 9 = 0x0200)
        ushort packed = RuleSerializer.PackFlagsAndCount(1, 16, 1);

        Assert.True((packed & 0x0200) != 0, "line bit 9 must be set");
        Assert.Equal(1, packed & 0x0F);              // shotLogic preserved
        Assert.Equal(16, (packed >> 4) & 0x1F);      // numTerms preserved (bits 4-8)
        Assert.Equal(0x0301, packed);                // 0x0200 | 0x0100 | 0x0001
    }

    [Theory]
    [InlineData(1, 16, 0)]   // boundary numTerms=16, line 0
    [InlineData(1, 16, 1)]   // boundary numTerms=16, line 1 — must not leak into numTerms
    [InlineData(0, 1, 1)]    // INHIBIT, 1 term, line 1
    [InlineData(1, 8, 0)]    // SHOOT, 8 terms, line 0
    public void UnpackFlagsAndCount_RoundTripsShotLogicNumTermsAndLine(int shotLogic, int numTerms, int line)
    {
        ushort packed = RuleSerializer.PackFlagsAndCount(shotLogic, numTerms, line);

        (int outShotLogic, int outNumTerms, int outLine) = RuleSerializer.UnpackFlagsAndCount(packed);

        Assert.Equal(shotLogic, outShotLogic);
        Assert.Equal(numTerms, outNumTerms);   // line must NOT leak into numTerms
        Assert.Equal(line, outLine);
    }

    /*--- Serialize header version ---*/

    [Fact]
    public void Serialize_EmitsHeaderVersion3()
    {
        var rule = _compiler.Compile("C1.F0");

        byte[] data = _serializer.Serialize(new[] { rule });

        Assert.Equal(3, data[2]);   // version byte
    }

    /*--- Round-trip ShootLine ---*/

    [Fact]
    public void RoundTrip_PreservesShootLine1()
    {
        var rule = _compiler.Compile("C1.F0");
        rule.ShootLine = 1;   // Upper line

        byte[] data = _serializer.Serialize(new[] { rule });
        var restored = _serializer.Deserialize(data);

        Assert.Single(restored);
        Assert.Equal(1, restored[0].ShootLine);
    }

    [Fact]
    public void RoundTrip_DefaultShootLine0_Unchanged()
    {
        var rule = _compiler.Compile("C1.F0");   // default ShootLine = 0 (Lower)

        byte[] data = _serializer.Serialize(new[] { rule });
        var restored = _serializer.Deserialize(data);

        Assert.Single(restored);
        Assert.Equal(0, restored[0].ShootLine);
    }

    [Fact]
    public void RoundTrip_TwoRulesDifferentShootLine_SurviveIndependently()
    {
        var lower = _compiler.Compile("C1.F0");
        lower.ShootLine = 0;
        var upper = _compiler.Compile("C2.F1");
        upper.ShootLine = 1;

        byte[] data = _serializer.Serialize(new[] { lower, upper });
        var restored = _serializer.Deserialize(data);

        Assert.Equal(2, restored.Length);
        Assert.Equal(0, restored[0].ShootLine);
        Assert.Equal(1, restored[1].ShootLine);
    }
}
