using System.Collections;
using System.ComponentModel;
using System.Globalization;
using Zerox.Core.ComponentModel;
using Zerox.Core.Validation;

namespace Zerox.Core.Tests.Validation;

public sealed class ValidatorTests
{
    // The old dialog model: it holds a validator, forwards INotifyDataErrorInfo and re-validates a changed property.
    public class TestModel : NotifyingObject, INotifyDataErrorInfo
    {
        private readonly Validator<TestModel> _validator;
        private string _strProperty = string.Empty;

        public TestModel()
            : this(new ValidationRules<TestModel>
            {
                { nameof(StrProperty), "StrProperty cannot be empty.", x => !string.IsNullOrEmpty(x.StrProperty) },
                { nameof(StrProperty), "StrProperty must be {5, 10} len.", x => x.StrProperty == default || x.StrProperty.Length is >= 5 and <= 10 },
                {
                    nameof(StrProperty),
                    (TestModel x, out object? err) =>
                    {
                        err = "StrProperty must must start with B.";
                        return x.StrProperty == default || x.StrProperty.StartsWith('B');
                    }
                },
            })
        {
        }

        protected TestModel(ValidationRules<TestModel> rules)
        {
            _validator = new(this, rules);
            _validator.ErrorsChanged += (_, e) => ErrorsChanged?.Invoke(this, e);
        }

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public string StrProperty
        {
            get => _strProperty;
            set => SetProperty(ref _strProperty, value);
        }

        public bool HasErrors => _validator.HasErrors;

        public bool Validate() => _validator.Validate();

        public IEnumerable GetErrors(string? propertyName) => _validator.GetErrors(propertyName);

        protected override void OnPropertyChanged(string? propertyName)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName is not null)
                _validator.Validate(propertyName);
        }
    }

    public sealed class TestModelNoRules() : TestModel(new ValidationRules<TestModel>());

    public sealed class Child : IValidatable
    {
        private readonly List<object> _errors = [];

        public string Value { get; set; } = "";

        public ValidationParameters? Received { get; private set; }

        public bool Validate(ValidationParameters? parameters = null)
        {
            Received = parameters;
            _errors.Clear();
            if (Value.Length == 0)
                _errors.Add("Value is empty");
            if (parameters?.Get<string>("limit") is { } limit && Value.Length > int.Parse(limit, CultureInfo.InvariantCulture))
                _errors.Add("Value is too long");
            return _errors.Count == 0;
        }

        public IReadOnlyList<object> GetAllErrors() => [.. _errors];
    }

    public sealed class Parent
    {
        public Child One { get; set; } = new();

        public List<Child> Many { get; } = [new(), new()];
    }

    public sealed class Pair
    {
        public string A { get; set; } = "";

        public string B { get; set; } = "";
    }

    private static string Joined(TestModel model) => string.Join(" ", model.GetErrors("StrProperty").Cast<string>());

    [Fact]
    public void Test_Valid()
    {
        var tm = new TestModel();
        var hasError = false;
        tm.ErrorsChanged += (_, _) => hasError = true;

        Assert.False(tm.Validate());
        Assert.True(hasError);
        Assert.True(tm.HasErrors);
        Assert.Equal("StrProperty cannot be empty. StrProperty must be {5, 10} len. StrProperty must must start with B.", Joined(tm));

        tm.StrProperty = "aaa";
        Assert.True(tm.HasErrors);
        Assert.Equal("StrProperty must be {5, 10} len. StrProperty must must start with B.", Joined(tm));

        tm.StrProperty = "aaaaaa";
        Assert.True(tm.HasErrors);
        Assert.Equal("StrProperty must must start with B.", Joined(tm));

        tm.StrProperty = "Baaaaaa";
        Assert.False(tm.HasErrors);
        Assert.Empty(Joined(tm));
    }

    [Fact]
    public void Test_NoRules()
    {
        var tm = new TestModelNoRules();
        var hasError = false;
        tm.ErrorsChanged += (_, _) => hasError = true;

        Assert.False(tm.HasErrors);
        Assert.Empty(tm.GetErrors("StrProperty").Cast<object>());
        Assert.True(tm.Validate());
        Assert.False(hasError);
    }

    [Fact]
    public void Should_ValidateTheChildren_WhenPropertiesAreValidatable()
    {
        var parent = new Parent();
        var validator = new Validator<Parent>(parent, []);

        validator.Validate().Should().BeFalse();
        validator.GetErrors("One").Should().Equal("Value is empty");
        validator.GetErrors("Many").Should().Equal("Value is empty", "Value is empty");

        parent.One.Value = "x";
        foreach (var child in parent.Many)
            child.Value = "y";

        validator.Validate().Should().BeTrue();
        validator.GetAllErrors().Should().BeEmpty();
    }

    [Fact]
    public void Should_PassTheParametersDown_WhenChildrenAreValidated()
    {
        var parent = new Parent();
        parent.One.Value = "abcd";
        foreach (var child in parent.Many)
            child.Value = "ok";
        var parameters = new ValidationParameters();
        parameters.Set("limit", "3");
        var validator = new Validator<Parent>(parent, []);

        validator.Validate(parameters).Should().BeFalse();

        parent.One.Received.Should().BeSameAs(parameters);
        validator.Parameters.Should().BeSameAs(parameters);
        validator.GetErrors("One").Should().Equal("Value is too long");
    }

    [Fact]
    public void Should_RaiseErrorsChangedOnce_WhenAPropertyChangesItsErrors()
    {
        var pair = new Pair();
        var validator = new Validator<Pair>(pair, new ValidationRules<Pair> { { "A", "A is empty", p => p.A.Length > 0 } });
        var raised = new List<string?>();
        validator.ErrorsChanged += (_, e) => raised.Add(e.PropertyName);

        validator.Validate();
        validator.Validate();
        raised.Should().Equal("A");

        pair.A = "x";
        validator.Validate("A").Should().BeTrue();

        raised.Should().Equal("A", "A");
        validator.HasErrors.Should().BeFalse();
    }
}
