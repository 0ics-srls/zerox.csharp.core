using Zerox.Core.Validation;

namespace Zerox.Core.Tests.Validation;

public sealed class ValidationRuleTests
{
    public sealed record Subject(string A, string B);

    [Fact]
    public void Should_ReturnTheErrorOrNull_WhenARuleIsApplied()
    {
        var simple = new ValidationRule<Subject>("A", "A is empty", s => s.A.Length > 0);
        var withError = new ValidationRule<Subject>("B", (Subject s, out object? error) =>
        {
            error = $"B is {s.B.Length} long";
            return s.B.Length > 2;
        });

        simple.Apply(new Subject("", "")).Should().Be("A is empty");
        simple.Apply(new Subject("a", "")).Should().BeNull();
        withError.Apply(new Subject("", "x")).Should().Be("B is 1 long");
        withError.Apply(new Subject("", "xyz")).Should().BeNull();
    }

    [Fact]
    public void Should_Throw_WhenARuleWithErrorFailsWithoutAnError()
    {
        var rule = new ValidationRule<Subject>("A", (Subject _, out object? error) =>
        {
            error = null;
            return false;
        });

        FluentActions.Invoking(() => rule.Apply(new Subject("", ""))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_ApplyOnlyTheRulesOfTheProperty_WhenANameIsGiven()
    {
        var rules = new ValidationRules<Subject>
        {
            { "A", "A is wrong", _ => false },
            { "B", "B is wrong", _ => false },
            { "", "the whole is wrong", _ => false },
        };
        var subject = new Subject("", "");

        rules.Apply(subject, "A").Should().Equal("A is wrong");
        rules.Apply(subject, "").Should().Equal("the whole is wrong");
        rules.Apply(subject).Should().Equal("A is wrong", "B is wrong", "the whole is wrong");
    }

    [Fact]
    public void Should_ListTheErrorsOneByLine_WhenAValidationExceptionIsBuilt()
    {
        var exception = new ValidationException(["a", "b"]);

        exception.Errors.Should().Equal("a", "b");
        exception.Message.Should().Be("a" + Environment.NewLine + "b");
    }
}
