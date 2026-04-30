using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Date;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteValidationOfDateStrings : IClassFixture<SuiteValidationOfDateStrings.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfDateStrings(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllStringFormatsIgnoreIntegers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllStringFormatsIgnoreFloats()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("13.7");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllStringFormatsIgnoreObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllStringFormatsIgnoreArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllStringFormatsIgnoreBooleans()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllStringFormatsIgnoreNulls()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-19\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInJanuary()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-01-31\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInJanuary()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-01-32\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith28DaysInFebruaryNormal()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2021-02-28\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith29DaysInFebruaryNormal()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2021-02-29\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith29DaysInFebruaryLeap()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-02-29\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith30DaysInFebruaryLeap()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-02-30\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInMarch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-03-31\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInMarch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-03-32\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith30DaysInApril()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-04-30\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith31DaysInApril()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-04-31\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInMay()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-05-31\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInMay()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-05-32\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith30DaysInJune()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-06-30\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith31DaysInJune()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-06-31\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInJuly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-07-31\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInJuly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-07-32\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInAugust()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-08-31\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInAugust()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-08-32\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith30DaysInSeptember()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-09-30\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith31DaysInSeptember()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-09-31\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInOctober()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-10-31\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInOctober()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-10-32\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith30DaysInNovember()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-11-30\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith31DaysInNovember()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-11-31\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInDecember()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-12-31\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInDecember()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-12-32\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAInvalidDateStringWithInvalidMonth()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-13-01\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidDateString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"06/19/1963\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOnlyRfc3339NotAllOfIso8601AreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2013-350\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonPaddedMonthDatesAreNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1998-1-20\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonPaddedDayDatesAreNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1998-01-1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidMonth()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1998-13-01\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidMonthDayCombination()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1998-04-31\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test2021IsNotALeapYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2021-02-29\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test2020IsALeapYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-02-29\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidNonAscii৪ABengali4()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-1৪\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIso8601NonRfc3339YyyymmddWithoutDashes20230328()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"20230328\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIso8601NonRfc3339WeekNumberImplicitDayOfWeek20230102()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2023-W01\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIso8601NonRfc3339WeekNumberWithDayOfWeek20230328()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2023-W13-2\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIso8601NonRfc3339WeekNumberRolloverToNextYear20230101()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2022W527\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidTimeStringInDateTimeFormat()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-11-28T23:55:45Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\date.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"date\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Date",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
