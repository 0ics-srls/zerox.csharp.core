using Zerox.Core.Text;

namespace Zerox.Core.Tests.Text;

public sealed class JsonDefaultsTests
{
    public enum SampleKind
    {
        First,
        Second,
    }

    public sealed record Sample(string Name, SampleKind Kind, int? Count);

    [Fact]
    public void Should_WriteNamesAsTheyAre_WhenSerializing()
        => new Sample("a", SampleKind.Second, null).ToJson().Should().Be("""{"Name":"a","Kind":"Second","Count":null}""");

    [Fact]
    public void Should_ReadEnumsByMemberName_WhenDeserializing()
        => """{"Name":"a","Kind":"Second","Count":3}""".FromJson<Sample>().Should().Be(new Sample("a", SampleKind.Second, 3));

    [Fact]
    public async Task Should_RoundTripThroughAStream_WhenUsingTheAsyncMethods()
    {
        var sample = new Sample("b", SampleKind.First, 7);
        using var stream = new MemoryStream();

        await stream.ToJsonAsync(sample, TestContext.Current.CancellationToken);
        stream.Position = 0;

        (await stream.FromJsonAsync<Sample>(TestContext.Current.CancellationToken)).Should().Be(sample);
    }
}
