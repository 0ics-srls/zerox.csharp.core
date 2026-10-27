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

    internal sealed class FieldSample
    {
        public string Name = "";
        public int Count;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string> WriteAsync<T>(T value)
    {
        using var stream = new MemoryStream();
        await stream.ToJsonAsync(value, Ct);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static async Task<T?> ReadAsync<T>(string json)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return await stream.FromJsonAsync<T>(Ct);
    }

    [Fact]
    public async Task Should_WriteNamesAsTheyAre_WhenSerializing()
        => (await WriteAsync(new Sample("a", SampleKind.Second, null))).Should().Be("""{"Name":"a","Kind":"Second","Count":null}""");

    [Fact]
    public async Task Should_ReadEnumsByMemberName_WhenDeserializing()
        => (await ReadAsync<Sample>("""{"Name":"a","Kind":"Second","Count":3}""")).Should().Be(new Sample("a", SampleKind.Second, 3));

    [Fact]
    public async Task Should_RoundTripThroughAStream_WhenUsingTheAsyncMethods()
    {
        var sample = new Sample("b", SampleKind.First, 7);
        using var stream = new MemoryStream();

        await stream.ToJsonAsync(sample, TestContext.Current.CancellationToken);
        stream.Position = 0;

        (await stream.FromJsonAsync<Sample>(TestContext.Current.CancellationToken)).Should().Be(sample);
    }

    [Fact]
    public async Task Should_WriteAndReadPublicFields_WhenUsingTheJsonDefaults()
    {
        var json = await WriteAsync(new FieldSample { Name = "a", Count = 3 });
        var back = await ReadAsync<FieldSample>(json);

        json.Should().Be("""{"Name":"a","Count":3}""");
        back!.Name.Should().Be("a");
        back.Count.Should().Be(3);
    }
}
