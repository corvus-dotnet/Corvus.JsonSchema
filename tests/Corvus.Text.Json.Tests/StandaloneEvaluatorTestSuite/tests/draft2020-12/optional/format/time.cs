using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Time;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteValidationOfTimeStrings : IClassFixture<SuiteValidationOfTimeStrings.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfTimeStrings(Fixture fixture)
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
    public void TestAValidTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06Z\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidTimeStringWithExtraLeadingZeros()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"008:030:006Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidTimeStringWithNoLeadingZeroForSingleDigit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"8:3:6Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHourMinuteSecondMustBeTwoDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"8:0030:6Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidTimeStringWithSecondFraction()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:20:50.52Z\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidTimeStringWithPreciseSecondFraction()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06.283185Z\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidTimeStringWithPlusOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06+00:20\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidTimeStringWithMinusOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06-08:00\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHourMinuteInTimeOffsetMustBeTwoDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06-8:000\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidTimeStringWithCaseInsensitiveZ()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06z\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidTimeStringWithInvalidHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"24:00:00Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidTimeStringWithInvalidMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"00:60:00Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidTimeStringWithInvalidSecond()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"00:00:61Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidTimeStringWithInvalidTimeNumoffsetHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"01:02:03+24:00\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidTimeStringWithInvalidTimeNumoffsetMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"01:02:03+00:60\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidTimeStringWithInvalidTimeWithBothZAndNumoffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"01:02:03Z+00:30\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidOffsetIndicator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06 PST\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOnlyRfc3339NotAllOfIso8601AreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"01:01:01,1111\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoTimeOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"12:00:00\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoTimeOffsetWithSecondFraction()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"12:00:00.52\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidNonAscii২ABengali2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1২:00:00Z\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOffsetNotStartingWithPlusOrMinus()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06#00:20\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestContainsLetters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ab:cd:ef\"");
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
                "tests\\draft2020-12\\optional\\format\\time.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"time\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Time",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
