using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Duration;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteValidationOfDurationStrings : IClassFixture<SuiteValidationOfDurationStrings.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfDurationStrings(Fixture fixture)
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
    public void TestAValidDurationString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P4DT12H30M5S\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidDurationString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT1D\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMustStartWithP()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"4DT12H30M5S\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoElementsPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoTimeElementsPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1YT\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoDateOrTimeElementsPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestElementsOutOfOrder()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P2D1Y\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingTimeSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1D2H\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTimeElementInTheDatePosition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P2S\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFourYearsDuration()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P4Y\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroTimeInSeconds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT0S\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroTimeInDays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P0D\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOneMonthDuration()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1M\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOneMinuteDuration()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT1M\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOneAndAHalfDaysInHours()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT36H\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOneAndAHalfDaysInDaysAndHours()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1DT12H\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTwoWeeks()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P2W\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWeeksCannotBeCombinedWithOtherUnits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1Y2W\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidNonAscii২ABengali2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P২Y\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestElementWithoutUnit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format\\duration.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"duration\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Duration",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
