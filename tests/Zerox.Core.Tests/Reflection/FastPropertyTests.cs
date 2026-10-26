using System.Collections.Concurrent;
using System.Reflection;
using Zerox.Core.Reflection;

namespace Zerox.Core.Tests.Reflection;

public sealed class FastPropertyTests
{
    public sealed class Sample
    {
        private int _written;

        public int Count { get; set; }

        public int Hidden { get; private set; }

        public int WriteOnly
        {
            set => _written = value;
        }

        public int Written => _written;
    }

    private static PropertyInfo Info(string name) => typeof(Sample).GetProperty(name)!;

    [Fact]
    public void Should_ReadAndWrite_WhenThePropertyIsPublic()
    {
        var sample = new Sample { Count = 3 };
        var count = FastProperty.For<Sample, int>(Info("Count"));

        count.Get(sample).Should().Be(3);
        count.Set(sample, 9);
        sample.Count.Should().Be(9);
    }

    [Fact]
    public void Should_ConvertTheValue_WhenTheTypesAreObject()
    {
        object sample = new Sample { Count = 5 };
        var count = FastProperty.For<object, object>(Info("Count"));

        count.Get(sample).Should().Be(5);
        count.Set(sample, 11);
        ((Sample)sample).Count.Should().Be(11);
    }

    [Fact]
    public void Should_WriteAPrivateSetter_WhenPublicOnlyIsFalse()
    {
        var sample = new Sample();

        FastProperty.For<Sample, int>(Info("Hidden"), publicOnly: false).Set(sample, 4);
        sample.Hidden.Should().Be(4);
        FluentActions.Invoking(() => FastProperty.For<Sample, int>(Info("Hidden")).Set(sample, 5))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_ReturnTheSameAccessor_WhenAskedTwice()
    {
        var info = Info("Count");
        var accessors = new ConcurrentBag<FastProperty<Sample, int>>();

        Parallel.For(0, 8, _ => accessors.Add(FastProperty.For<Sample, int>(info)));

        FastProperty.For<Sample, int>(info).Should().BeSameAs(FastProperty.For<Sample, int>(info));
        accessors.Distinct().Should().ContainSingle();
    }

    [Fact]
    public void Should_Throw_WhenThePropertyHasNoGetter()
    {
        var sample = new Sample();
        var writeOnly = FastProperty.For<Sample, int>(Info("WriteOnly"));

        writeOnly.CanRead.Should().BeFalse();
        writeOnly.Set(sample, 6);
        sample.Written.Should().Be(6);
        FluentActions.Invoking(() => writeOnly.Get(sample)).Should().Throw<InvalidOperationException>();
    }
}
