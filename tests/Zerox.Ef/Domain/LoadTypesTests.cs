using System.Text.Json;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain.Tests;

// The load types of Zerox.Ef.Core travel exactly as DevExtreme's: the options a client sends today bind, the
// result is written with DevExtreme's names (measured on 5.1.0).
public sealed class LoadTypesTests
{
    // The JSON settings of the API: names as declared.
    private static readonly JsonSerializerOptions AsIs = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = null };

    [Fact]
    public void Should_BindTheOptionsAClientSendsToday()
    {
        const string json = """
            {"Skip":10,"Take":5,"RequireTotalCount":true,"Filter":["Kind","=","Large"],
             "Sort":[{"Selector":"Quantity","Desc":true}],
             "Group":[{"Selector":"At","GroupInterval":"month","IsExpanded":false}],
             "TotalSummary":[{"Selector":"Quantity","SummaryType":"sum"}],"Select":["Name"]}
            """;

        var options = JsonSerializer.Deserialize<LoadOptions>(json, AsIs);
        options.Should().NotBeNull();

        options.Skip.Should().Be(10);
        options.Take.Should().Be(5);
        options.RequireTotalCount.Should().BeTrue();
        options.Filter.Should().HaveCount(3);
        var sort = options.Sort.Should().ContainSingle().Which;
        sort.Selector.Should().Be("Quantity");
        sort.Desc.Should().BeTrue();
        var group = options.Group.Should().ContainSingle().Which;
        group.Selector.Should().Be("At");
        group.GroupInterval.Should().Be("month");
        group.IsExpanded.Should().BeFalse();
        options.TotalSummary.Should().ContainSingle().Which.SummaryType.Should().Be("sum");
        options.Select.Should().Equal("Name");
    }

    // A client may send a number's group width as a JSON number, as DevExtreme reads it.
    [Fact]
    public void Should_BindANumericGroupInterval()
    {
        const string json = """{"Group":[{"Selector":"Quantity","GroupInterval":10},{"Selector":"At","GroupInterval":"month"}]}""";

        var options = JsonSerializer.Deserialize<LoadOptions>(json, AsIs);
        options.Should().NotBeNull();

        options.Group.Should().NotBeNull();
        options.Group.Select(g => g.GroupInterval).Should().Equal("10", "month");
    }

    [Fact]
    public void Should_WriteTheResultWithDevExtremeNames()
    {
        var loaded = new Loaded { Data = [1, 2], TotalCount = 2, GroupCount = -1 };

        var json = JsonSerializer.Serialize(loaded, AsIs);

        json.Should().Be("""{"data":[1,2],"totalCount":2,"groupCount":-1}""");
    }

    [Fact]
    public void Should_WriteATextValueWithItsDeclaredNames_WhenSerialized()
    {
        var json = JsonSerializer.Serialize(new TextValue("pear", 2), AsIs);

        json.Should().Be("""{"Value":"pear","Count":2}""");
    }

    [Fact]
    public void Should_WriteAGroupWithDevExtremeNames()
    {
        var full = new LoadGroup { Key = "a", Items = null, Count = 3, Summary = [6] };
        var bare = new LoadGroup { Key = "a", Items = null };

        var json = (JsonSerializer.Serialize(full, AsIs), JsonSerializer.Serialize(bare, AsIs));

        json.Should().Be(("""{"key":"a","items":null,"count":3,"summary":[6]}""", """{"key":"a","items":null}"""));
    }
}
