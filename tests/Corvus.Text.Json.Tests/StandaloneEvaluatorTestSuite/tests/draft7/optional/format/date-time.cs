using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.Format.DateTime;

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteValidationOfDateTimeStrings : IClassFixture<SuiteValidationOfDateTimeStrings.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfDateTimeStrings(Fixture fixture)
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
    public void TestAValidDateTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-19T08:30:06.283185Z\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateTimeStringWithoutSecondFraction()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-19T08:30:06Z\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateTimeStringWithPlusOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1937-01-01T12:00:27.87+00:20\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDateTimeStringWithMinusOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1990-12-31T15:59:50.123-08:00\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidDayInDateTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1990-02-31T15:59:59.123-08:00\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidOffsetInDateTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1990-12-31T15:59:59-24:00\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidClosingZAfterTimeZoneOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-19T08:30:06.28123+01:00Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidDateTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"06/19/1963 08:30:06 PST\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestCaseInsensitiveTAndZ()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-19t08:30:06.283185z\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOnlyRfc3339NotAllOfIso8601AreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2013-350T01:01:01\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidNonPaddedMonthDates()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-6-19T08:30:06.283185Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidNonPaddedDayDates()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-1T08:30:06.283185Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidNonAscii৪ABengali4InDatePortion()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-1৪T00:00:00Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidNonAscii৪ABengali4InTimePortion()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-11T0৪:00:00Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidExtendedYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"+11963-06-19T08:30:06.283185Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\optional\\format\\date-time.json",
                "{ \"format\": \"date-time\" }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Format.DateTime",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
