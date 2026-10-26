
namespace Zerox.Dnf.Tests;

public class SerializerV2Tests
{
    private readonly RuleCompiler _compiler = new();
    private readonly RuleSerializer _serializer = new();

    /*--- Pack/Unpack Tests ---*/

    [Theory]
    [InlineData(1, 1, 0x0011)]   // SHOOT, 1 term
    [InlineData(1, 2, 0x0021)]   // SHOOT, 2 terms
    [InlineData(0, 1, 0x0010)]   // INHIBIT, 1 term
    [InlineData(1, 16, 0x0101)]  // SHOOT, 16 terms
    public void PackFlagsAndCount_PacksCorrectly(int shotLogic, int numTerms, ushort expected)
    {
        ushort packed = RuleSerializer.PackFlagsAndCount(shotLogic, numTerms);
        Assert.Equal(expected, packed);
    }

    [Theory]
    [InlineData(0x0011, 1, 1)]
    [InlineData(0x0021, 1, 2)]
    [InlineData(0x0010, 0, 1)]
    [InlineData(0x0101, 1, 16)]
    public void UnpackFlagsAndCount_UnpacksCorrectly(ushort packed, int expectedShotLogic, int expectedNumTerms)
    {
        (int shotLogic, int numTerms, _) = RuleSerializer.UnpackFlagsAndCount(packed);
        Assert.Equal(expectedShotLogic, shotLogic);
        Assert.Equal(expectedNumTerms, numTerms);
    }

    /*--- Serialize Tests ---*/

    [Fact]
    public void Serialize_EmptyRules_ReturnsHeaderOnly()
    {
        var rules = Array.Empty<CompiledRule>();

        byte[] data = _serializer.Serialize(rules);

        Assert.Equal(4, data.Length);
        Assert.Equal((byte)'R', data[0]);
        Assert.Equal((byte)'L', data[1]);
        Assert.Equal(3, data[2]);  // Version 3
        Assert.Equal(0, data[3]);  // 0 rules
    }

    [Fact]
    public void Serialize_SingleRule_DefaultMetadata()
    {
        var rule = _compiler.Compile("C1.F0");

        byte[] data = _serializer.Serialize(new[] { rule });

        // Header (4) + ageAdjust (2) + flagsAndCount (2) + 1 term (16) = 24 bytes
        Assert.Equal(24, data.Length);
        Assert.Equal(3, data[2]);  // Version 3

        // Default ageAdjust = 0
        Assert.Equal(0, BitConverter.ToInt16(data, 4));

        // Default: SHOOT (1) + 1 term = 0x0011
        Assert.Equal(0x0011, BitConverter.ToUInt16(data, 6));
    }

    [Fact]
    public void Serialize_WithMetadata_SerializesCorrectly()
    {
        var rule = _compiler.Compile("C1.F0");
        rule.AgeAdjust = -10;
        rule.IsShootRule = false;  // INHIBIT

        byte[] data = _serializer.Serialize(new[] { rule });

        // ageAdjust = -10
        Assert.Equal(-10, BitConverter.ToInt16(data, 4));

        // INHIBIT (0) + 1 term = 0x0010
        Assert.Equal(0x0010, BitConverter.ToUInt16(data, 6));
    }

    [Fact]
    public void Serialize_MultipleRules_PreservesOrder()
    {
        var rule1 = _compiler.Compile("C1.F0");
        rule1.AgeAdjust = 5;
        rule1.IsShootRule = false;

        var rule2 = _compiler.Compile("C2.F1");
        rule2.AgeAdjust = -3;
        rule2.IsShootRule = true;

        byte[] data = _serializer.Serialize(new[] { rule1, rule2 });

        Assert.Equal(2, data[3]);  // 2 rules

        // Rule 0: ageAdjust = 5
        Assert.Equal(5, BitConverter.ToInt16(data, 4));

        // Rule 1 offset: 4 (header) + 4 (rule0 meta) + 16 (rule0 term) = 24
        Assert.Equal(-3, BitConverter.ToInt16(data, 24));
    }

    /*--- Deserialize Tests ---*/

    [Fact]
    public void Deserialize_ValidV2_ParsesCorrectly()
    {
        byte[] data = new byte[]
        {
            (byte)'R', (byte)'L', 3, 1,  // Header (v3)
            0xF6, 0xFF,                   // ageAdjust = -10
            0x10, 0x00,                   // INHIBIT, 1 term
            0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,  // mustBeSet
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00   // mustBeClear
        };

        var rules = _serializer.Deserialize(data);

        Assert.Single(rules);
        Assert.Equal(-10, rules[0].AgeAdjust);
        Assert.False(rules[0].IsShootRule);
    }

    [Fact]
    public void Deserialize_V1Format_ThrowsException()
    {
        byte[] v1Data = new byte[] { (byte)'R', (byte)'L', 1, 0 };

        Assert.Throws<InvalidOperationException>(() => _serializer.Deserialize(v1Data));
    }

    /*--- Round-trip Tests ---*/

    [Fact]
    public void RoundTrip_PreservesAllData()
    {
        var original = _compiler.Compile("C1.F0 & C2.F1");
        original.AgeAdjust = 42;
        original.IsShootRule = true;

        byte[] data = _serializer.Serialize(new[] { original });
        var restored = _serializer.Deserialize(data);

        Assert.Single(restored);
        Assert.Equal(42, restored[0].AgeAdjust);
        Assert.True(restored[0].IsShootRule);
        Assert.Equal(original.Terms.Length, restored[0].Terms.Length);
    }

    [Fact]
    public void RoundTrip_PreservesTermData()
    {
        var rules = new[]
        {
            _compiler.Compile("C1.F1 & (C2.F2 | C2.F3)"),
            _compiler.Compile("C1.F0 & !C2.F0"),
            _compiler.Compile("C1.F5 & C3.F2"),
        };

        // Set different metadata for each rule
        rules[0].AgeAdjust = 10;
        rules[0].IsShootRule = true;
        rules[1].AgeAdjust = -5;
        rules[1].IsShootRule = false;
        rules[2].AgeAdjust = 0;
        rules[2].IsShootRule = true;

        var serialized = _serializer.Serialize(rules);
        var deserialized = _serializer.Deserialize(serialized);

        Assert.Equal(rules.Length, deserialized.Length);

        for (int i = 0; i < rules.Length; i++)
        {
            Assert.Equal(rules[i].NumTerms, deserialized[i].NumTerms);
            Assert.Equal(rules[i].AgeAdjust, deserialized[i].AgeAdjust);
            Assert.Equal(rules[i].IsShootRule, deserialized[i].IsShootRule);

            for (int t = 0; t < rules[i].NumTerms; t++)
            {
                Assert.Equal(rules[i].Terms[t].MustBeSet, deserialized[i].Terms[t].MustBeSet);
                Assert.Equal(rules[i].Terms[t].MustBeClear, deserialized[i].Terms[t].MustBeClear);
            }
        }
    }

    [Fact]
    public void RoundTrip_EvaluatesSameAfterRestore()
    {
        var original = _compiler.Compile("C1.F1 & (C2.F2 | C2.F3)");
        var serialized = _serializer.Serialize(new[] { original });
        var deserialized = _serializer.Deserialize(serialized)[0];

        // Test with several inputs
        var testInputs = new ushort[][]
        {
            new ushort[] { 0x0002, 0x0004, 0, 0 },  // C1.F1=1, C2.F2=1
            new ushort[] { 0x0002, 0x0008, 0, 0 },  // C1.F1=1, C2.F3=1
            new ushort[] { 0x0002, 0x0000, 0, 0 },  // C1.F1=1, C2=0
            new ushort[] { 0x0000, 0x0004, 0, 0 },  // C1.F1=0, C2.F2=1
        };

        foreach (var input in testInputs)
        {
            Assert.Equal(original.Evaluate(input), deserialized.Evaluate(input));
        }
    }
}
