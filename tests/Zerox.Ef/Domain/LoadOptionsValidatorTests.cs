using System.Collections;
using System.Reflection;
using DevExtreme.AspNet.Data;

namespace Zerox.Ef.Domain.Tests;

// The validator checks the whole options object before DevExtreme, and a refusal says what is wrong and
// how it is written right. Measured on DevExtreme 5.1.0: for a value that does not convert it does not
// throw, it returns 0 rows silently; a 4-element condition and an "xor" return every row. The filter is
// built as it comes out of model binding: lists of strings, numbers and lists.
public sealed class LoadOptionsValidatorTests
{
    private static List<object?> L(params object?[] elements) => [.. elements];

    private static DataSourceLoadOptionsBase WithFilter(IList filter) => new() { Filter = filter };

    private static string Refusal<T>(DataSourceLoadOptionsBase options)
    {
        var validate = () => LoadOptionsValidator.Validate<T>(options);
        return validate.Should().Throw<InvalidRequestException>().Which.Message;
    }

    private static void Passes<T>(DataSourceLoadOptionsBase options)
    {
        var validate = () => LoadOptionsValidator.Validate<T>(options);
        validate.Should().NotThrow();
    }

    // --- fields ---

    [Fact]
    public void Should_NameTypeOptionAndFields_WhenAFilterFieldDoesNotExist()
    {
        var message = Refusal<Item>(WithFilter(L("Nme", "=", "x")));

        message.Should().Contain("'Nme'").And.Contain("Item").And.Contain("(in Filter)");
        message.Should().Contain("Fields:").And.Contain("Name").And.Contain("Quantity");
        message.Should().Contain("navigations:").And.Contain("Box");
    }

    [Fact]
    public void Should_ListTheFieldsOfTheNavigatedType_WhenASegmentAfterANavigationDoesNotExist()
    {
        var message = Refusal<Item>(WithFilter(L("Box.Nope", "=", "x")));

        message.Should().Contain("'Nope'").And.Contain("Box").And.Contain("Label").And.Contain("Opened");
    }

    [Fact]
    public void Should_NotListReservedFields_WhenListingAvailableFields()
    {
        var message = Refusal<Item>(WithFilter(L("Xyz", "=", "x")));

        message.Should().Contain("Name").And.NotContain("Secret");
    }

    [Theory]
    [InlineData("Sort")]
    [InlineData("Group")]
    [InlineData("TotalSummary")]
    [InlineData("GroupSummary")]
    [InlineData("Select")]
    [InlineData("PreSelect")]
    [InlineData("DefaultSort")]
    [InlineData("PrimaryKey")]
    public void Should_NameTheOption_WhenAFieldDoesNotExistInSortGroupSummarySelectPreSelectDefaultSortPrimaryKey(string option)
    {
        var options = new DataSourceLoadOptionsBase();
        switch (option)
        {
            case "Sort": options.Sort = [new SortingInfo { Selector = "Missing" }]; break;
            case "Group": options.Group = [new GroupingInfo { Selector = "Missing" }]; break;
            case "TotalSummary": options.TotalSummary = [new SummaryInfo { Selector = "Missing", SummaryType = "count" }]; break;
            case "GroupSummary": options.GroupSummary = [new SummaryInfo { Selector = "Missing", SummaryType = "count" }]; break;
            case "Select": options.Select = ["Missing"]; break;
            case "PreSelect": options.PreSelect = ["Missing"]; break;
            case "DefaultSort": options.DefaultSort = "Missing"; break;
            case "PrimaryKey": options.PrimaryKey = ["Missing"]; break;
        }

        Refusal<Item>(options).Should().Contain($"(in {option})").And.Contain("'Missing'");
    }

    [Fact]
    public void Should_Pass_WhenFieldsAndNavigationsUseAnyCase()
    {
        Passes<Item>(WithFilter(L("quantity", "=", 2)));
        Passes<Item>(WithFilter(L("BOX.label", "=", "b1")));
    }

    // --- Selector ---

    [Fact]
    public void Should_Throw_WhenSortGroupOrSummaryHasNoSelector_ButCountPasses()
    {
        Refusal<Item>(new() { Sort = [new SortingInfo { Desc = true }] }).Should().Contain("In Sort the Selector is missing");
        Refusal<Item>(new() { Group = [new GroupingInfo()] }).Should().Contain("In Group the Selector is missing");
        foreach (var type in new[] { "sum", "min", "max", "avg" })
            Refusal<Item>(new() { TotalSummary = [new SummaryInfo { SummaryType = type }] }).Should().Contain("the Selector is missing");

        Passes<Item>(new() { TotalSummary = [new SummaryInfo { SummaryType = "count" }] });
    }

    // --- collections ---

    [Fact]
    public void Should_NotEnterACollection_AndAllowItAtTheEndOnlyInSelect()
    {
        Refusal<Box>(WithFilter(L("Items.Name", "=", "a"))).Should().Contain("collection");
        Refusal<Box>(new() { Sort = [new SortingInfo { Selector = "Items.Name" }] }).Should().Contain("collection");
        Refusal<Box>(new() { Group = [new GroupingInfo { Selector = "Items.Name" }] }).Should().Contain("collection");
        Refusal<Box>(new() { TotalSummary = [new SummaryInfo { Selector = "Items.Quantity", SummaryType = "sum" }] }).Should().Contain("collection");
        Refusal<Box>(new() { Select = ["Items.Name"] }).Should().Contain("collection");

        Refusal<Box>(WithFilter(L("Items", "=", null))).Should().Contain("Select");
        Refusal<Box>(new() { Sort = [new SortingInfo { Selector = "Items" }] }).Should().Contain("collection");
        Refusal<Box>(new() { Group = [new GroupingInfo { Selector = "Items" }] }).Should().Contain("collection");
        Refusal<Box>(new() { TotalSummary = [new SummaryInfo { Selector = "Items", SummaryType = "count" }] }).Should().Contain("collection");

        Passes<Box>(new() { Select = ["Label", "Items"] });
        Passes<Box>(new() { PreSelect = ["Label", "Items"] });
    }

    // --- operators ---

    [Fact]
    public void Should_ListTheTenOperators_WhenTheOperatorIsUnknown_ButAnyCasePasses()
    {
        var message = Refusal<Item>(WithFilter(L("Name", "like", "a")));

        message.Should().Contain("'like'");
        foreach (var op in new[] { "=", "<>", "<", ">", "<=", ">=", "contains", "notcontains", "startswith", "endswith" })
            message.Should().Contain(op);
        Passes<Item>(WithFilter(L("Name", "CONTAINS", "a")));
        Passes<Item>(WithFilter(L("Name", "StartsWith", "a")));
    }

    [Theory]
    [InlineData("contains")]
    [InlineData("notcontains")]
    [InlineData("startswith")]
    [InlineData("endswith")]
    public void Should_Throw_WhenATextOperatorIsUsedOnANonTextField(string op)
    {
        Refusal<Item>(WithFilter(L("Quantity", op, 5))).Should().Contain("'Quantity'").And.Contain("text");
    }

    // --- grammar ---

    [Fact]
    public void Should_ShowTheRightForm_WhenAConditionHasTheWrongArity()
    {
        Refusal<Item>(WithFilter(L(L("Quantity", ">", 1), "and", L()))).Should().Contain("A condition is written");
        Refusal<Item>(WithFilter(L(L("Quantity", ">", 1), "and", L("Quantity")))).Should().Contain("A condition is written");
        Refusal<Item>(WithFilter(L("Quantity", ">", 1, 2))).Should().Contain("A condition is written");

        Passes<Item>(WithFilter(L("Kind", "Large")));   // the short form means "="
    }

    [Fact]
    public void Should_Throw_WhenTheFieldIsNotAString()
    {
        Refusal<Item>(WithFilter(L(1, "=", 2))).Should().Contain("A condition is written");
    }

    [Fact]
    public void Should_Throw_WhenTheSeparatorIsUnknown_ButImplicitAndPasses()
    {
        Refusal<Item>(WithFilter(L(L("Quantity", ">", 1), "xor", L("Quantity", "<", 9))))
            .Should().Contain("'xor'").And.Contain("and").And.Contain("or");

        Passes<Item>(WithFilter(L(L("Quantity", ">", 1), L("Quantity", "<", 9))));
    }

    [Fact]
    public void Should_Throw_WhenNegationHasNoCondition()
    {
        Refusal<Item>(WithFilter(L("!"))).Should().Contain("!");

        Passes<Item>(WithFilter(L("!", L("Quantity", ">", 1))));
    }

    // --- values ---

    [Theory]
    [InlineData(typeof(Item), "Quantity", "abc")]
    [InlineData(typeof(Item), "OwnerId", "abc")]
    [InlineData(typeof(Item), "At", "x")]
    [InlineData(typeof(Box), "Opened", "2026-13-45")]
    [InlineData(typeof(Box), "Opened", "2026-09-01T00:00:00Z")]
    [InlineData(typeof(Item), "Paid", "maybe")]
    [InlineData(typeof(Item), "Kind", "Medium")]
    [InlineData(typeof(Sample), "Code", "abc")]
    public void Should_Throw_WhenTheValueHasTheWrongType(Type type, string field, string value)
    {
        var validateOf = typeof(LoadOptionsValidator).GetMethod(nameof(LoadOptionsValidator.Validate));
        validateOf.Should().NotBeNull();
        var validate = () => validateOf
            .MakeGenericMethod(type).Invoke(null, [WithFilter(L(field, "=", value))]);

        var exception = validate.Should().Throw<TargetInvocationException>()
            .WithInnerException<InvalidRequestException>().Which;
        exception.Message.Should().Contain($"'{field}'");
    }

    [Fact]
    public void Should_ShowTheRightForm_WhenAValueIsWrong()
    {
        Refusal<Box>(WithFilter(L("Opened", "=", "2026-09-01T00:00:00Z"))).Should().Contain("yyyy-MM-dd");
        Refusal<Item>(WithFilter(L("At", "=", "x"))).Should().Contain("ISO 8601");
        Refusal<Item>(WithFilter(L("Kind", "=", "Medium"))).Should().Contain("Small").And.Contain("Large");
        Refusal<Item>(WithFilter(L("Paid", "=", "maybe"))).Should().Contain("true").And.Contain("false");
    }

    [Fact]
    public void Should_Throw_WhenAListIsGivenInsteadOfAValue()
    {
        Refusal<Item>(WithFilter(L("Kind", "=", L("Small", "Large")))).Should().Contain("or");
    }

    [Fact]
    public void Should_Pass_WhenTheValuesAreRight()
    {
        Passes<Item>(WithFilter(L("Quantity", "=", 5)));
        Passes<Item>(WithFilter(L("Quantity", ">", 5.5m)));
        Passes<Item>(WithFilter(L("Quantity", ">", "5")));
        Passes<Item>(WithFilter(L("Price", ">", 1.25m)));
        Passes<Box>(WithFilter(L("Opened", "=", "2026-09-01")));
        Passes<Item>(WithFilter(L("At", ">=", "2026-09-01")));
        Passes<Item>(WithFilter(L("At", ">=", "2026-09-01T10:00:00Z")));
        Passes<Item>(WithFilter(L("Kind", "=", "large")));
        Passes<Item>(WithFilter(L("Kind", "=", 1)));
        Passes<Item>(WithFilter(L("Kind", "=", "1")));
        Passes<Item>(WithFilter(L("Paid", "=", true)));
        Passes<Item>(WithFilter(L("Paid", "=", "false")));
        Passes<Item>(WithFilter(L("OwnerId", "=", null)));
        Passes<Item>(WithFilter(L("Note", "=", null)));
        Passes<Sample>(WithFilter(L("Code", "=", "8c1d6f5e-3b4a-4c2d-9e8f-1a2b3c4d5e6f")));
        Passes<Sample>(WithFilter(L("Weight", ">", 1.5m)));
        Passes<Sample>(WithFilter(L("Counter", ">", 3000000000m)));
    }

    [Fact]
    public void Should_Throw_WhenNullIsGivenToANonNullableField()
    {
        Refusal<Item>(WithFilter(L("Quantity", "=", null))).Should().Contain("'Quantity'");
    }

    // --- summaries and groups ---

    [Fact]
    public void Should_Throw_WhenTheSummaryIsUnknownOrOnTheWrongType()
    {
        var message = Refusal<Item>(new() { TotalSummary = [new SummaryInfo { Selector = "Quantity", SummaryType = "median" }] });
        foreach (var type in new[] { "sum", "min", "max", "avg", "count" })
            message.Should().Contain(type);
        Refusal<Item>(new() { TotalSummary = [new SummaryInfo { Selector = "Name", SummaryType = "sum" }] }).Should().Contain("'Name'");
        Refusal<Item>(new() { GroupSummary = [new SummaryInfo { Selector = "Name", SummaryType = "avg" }] }).Should().Contain("'Name'");

        Passes<Item>(new() { TotalSummary = [new SummaryInfo { Selector = "At", SummaryType = "count" }] });
        Passes<Item>(new() { TotalSummary = [new SummaryInfo { Selector = "At", SummaryType = "min" }, new SummaryInfo { Selector = "At", SummaryType = "max" }] });
    }

    [Theory]
    [InlineData("12.5")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("3000000000")]
    public void Should_RefuseANumericInterval_WhenItIsNoWholeNumberAboveZero(string width)
    {
        var message = Refusal<Item>(new() { Group = [new GroupingInfo { Selector = "Quantity", GroupInterval = width }] });

        message.Should().Contain("whole number above zero");
    }

    [Fact]
    public void Should_AllowGroupIntervalsOnlyOnTheRightType()
    {
        var message = Refusal<Item>(new() { Group = [new GroupingInfo { Selector = "At", GroupInterval = "week" }] });
        foreach (var interval in new[] { "year", "quarter", "month", "day", "dayOfWeek", "hour", "minute", "second" })
            message.Should().Contain(interval);
        Refusal<Item>(new() { Group = [new GroupingInfo { Selector = "Quantity", GroupInterval = "month" }] }).Should().Contain("'Quantity'");
        Refusal<Box>(new() { Group = [new GroupingInfo { Selector = "Opened", GroupInterval = "hour" }] }).Should().Contain("'Opened'");
        Refusal<Item>(new() { Group = [new GroupingInfo { Selector = "Name", GroupInterval = "2" }] }).Should().Contain("'Name'");

        Passes<Item>(new() { Group = [new GroupingInfo { Selector = "At", GroupInterval = "month" }] });
        Passes<Item>(new() { Group = [new GroupingInfo { Selector = "At", GroupInterval = "hour" }] });
        Passes<Box>(new() { Group = [new GroupingInfo { Selector = "Opened", GroupInterval = "month" }] });
        Passes<Item>(new() { Group = [new GroupingInfo { Selector = "Quantity", GroupInterval = "2" }] });
    }

    // --- right requests ---

    [Fact]
    public void Should_Pass_WhenTheRequestIsRight()
    {
        Passes<Item>(new()
        {
            Filter = L(
                L("At", ">=", "2026-01-01"),
                "and",
                L("!", L(L("Quantity", "<", 1), "or", L("Box.Label", "contains", "b")))),
            Sort = [new SortingInfo { Selector = "Box.Label", Desc = true }],
            TotalSummary = [new SummaryInfo { Selector = "Quantity", SummaryType = "sum" }],
            Group = [new GroupingInfo { Selector = "At", GroupInterval = "month" }],
            GroupSummary = [new SummaryInfo { Selector = "Price", SummaryType = "sum" }],
            Select = ["Name", "Box.Label"],
        });
    }

    [Fact]
    public void Should_Pass_WhenFilterIsNullOrEmptyAndOptionsAreEmpty()
    {
        Passes<Item>(new());
        Passes<Item>(WithFilter(L()));
        Passes<Item>(new() { Filter = null });
    }

    // --- safe options ---

    [Fact]
    public void Should_ForceAllowAsyncOverSyncFalse_WhenMadeSafe()
    {
        var options = new DataSourceLoadOptionsBase { AllowAsyncOverSync = true };

        Search.MakeSafe(options);

        options.AllowAsyncOverSync.Should().BeFalse();
    }

    [Fact]
    public void Should_NullRemoteGroupingAndRemoteSelect_WhenMadeSafe()
    {
        var options = new DataSourceLoadOptionsBase { RemoteGrouping = false, RemoteSelect = false };

        Search.MakeSafe(options);

        options.RemoteGrouping.Should().BeNull();
        options.RemoteSelect.Should().BeNull();
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(-5, -5)]
    public void Should_Throw_WhenSkipOrTakeIsNegative(int skip, int take)
    {
        var makeSafe = () => Search.MakeSafe(new DataSourceLoadOptionsBase { Skip = skip, Take = take });

        makeSafe.Should().Throw<InvalidRequestException>().Which.Message.Should().Contain("Skip").And.Contain("Take");
    }

    [Fact]
    public void Should_LeaveTheOtherOptionsAsTheyAre_WhenMadeSafe()
    {
        var filter = new List<object?> { "Quantity", ">", 2 };
        var options = new DataSourceLoadOptionsBase
        {
            RequireTotalCount = true,
            RequireGroupCount = true,
            Skip = 3,
            Take = 7,
            Sort = [new SortingInfo { Selector = "At", Desc = true }],
            Group = [new GroupingInfo { Selector = "Quantity", GroupInterval = "2" }],
            Filter = filter,
            Select = ["Name"],
            DefaultSort = "Id",
            StringToLower = true,
        };

        Search.MakeSafe(options);

        options.RequireTotalCount.Should().BeTrue();
        options.RequireGroupCount.Should().BeTrue();
        options.Skip.Should().Be(3);
        options.Take.Should().Be(7);
        options.Sort.Should().ContainSingle().Which.Selector.Should().Be("At");
        options.Group.Should().ContainSingle().Which.GroupInterval.Should().Be("2");
        options.Filter.Should().BeSameAs(filter);
        options.Select.Should().Equal("Name");
        options.DefaultSort.Should().Be("Id");
        options.StringToLower.Should().BeTrue();
    }
}
