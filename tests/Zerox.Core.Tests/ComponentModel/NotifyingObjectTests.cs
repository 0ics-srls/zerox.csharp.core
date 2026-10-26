using Zerox.Core.ComponentModel;

namespace Zerox.Core.Tests.ComponentModel;

public sealed class NotifyingObjectTests
{
    public class Sample : NotifyingObject
    {
        private string _name = "";

        public string Name
        {
            get => _name;
            set => LastSet = SetProperty(ref _name, value);
        }

        public bool LastSet { get; private set; }

        public void RaiseMany(string first, string second) => NotifyPropertiesChanged(first, second);

        public void RaiseAll() => NotifyPropertyChanged(null);
    }

    public sealed class Recording : Sample
    {
        public List<string?> Seen { get; } = [];

        protected override void OnPropertyChanged(string? propertyName) => Seen.Add(propertyName);
    }

    private static List<string?> Listen(NotifyingObject source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    [Fact]
    public void Should_NotifyOnce_WhenTheValueChanges()
    {
        var sample = new Sample();
        var names = Listen(sample);

        sample.Name = "a";

        sample.LastSet.Should().BeTrue();
        names.Should().Equal("Name");
        sample.Name.Should().Be("a");
    }

    [Fact]
    public void Should_NotNotify_WhenTheValueIsTheSame()
    {
        var sample = new Sample { Name = "a" };
        var names = Listen(sample);

        sample.Name = "a";

        sample.LastSet.Should().BeFalse();
        names.Should().BeEmpty();
    }

    [Fact]
    public void Should_NotifyEveryName_WhenSeveralAreGiven()
    {
        var sample = new Sample();
        var names = Listen(sample);

        sample.RaiseMany("A", "B");

        names.Should().Equal("A", "B");
    }

    [Fact]
    public void Should_NotifyEveryProperty_WhenTheNameIsNull()
    {
        var sample = new Sample();
        var names = Listen(sample);

        sample.RaiseAll();

        names.Should().Equal([null]);
    }

    [Fact]
    public void Should_PassEveryNotificationThroughOnPropertyChanged_WhenASubclassOverridesIt()
    {
        var recording = new Recording();
        var names = Listen(recording);

        recording.Name = "a";

        recording.Seen.Should().Equal("Name");
        names.Should().BeEmpty();
    }

    [Fact]
    public void Should_LetTheHandlerExceptionReachTheSetter_WhenAHandlerThrows()
    {
        var sample = new Sample();
        sample.PropertyChanged += (_, _) => throw new InvalidOperationException("handler");

        FluentActions.Invoking(() => sample.Name = "a").Should().Throw<InvalidOperationException>().WithMessage("handler");
    }
}
