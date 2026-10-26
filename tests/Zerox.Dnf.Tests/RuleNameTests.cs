
namespace Zerox.Dnf.Tests;

// Task 122 Block 01 — CompiledRule carries a technician-facing Name for UI/diagnostics.
// The name is NOT serialized to HW (see SerializerV2Tests), it only feeds the virtual-EV tooltip.
public class RuleNameTests
{
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void CompiledRule_Name_DefaultsToRule()
    {
        var rule = _compiler.Compile(ExpressionNode.Operand("C1.F1"));

        Assert.Equal("Rule", rule.Name);
    }

    [Fact]
    public void CompiledRule_Name_IsAssignable()
    {
        var rule = _compiler.Compile(ExpressionNode.Operand("C1.F1"));

        rule.Name = "Reject dark spots";

        Assert.Equal("Reject dark spots", rule.Name);
    }
}
