using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Uuid;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUuidFormat : IClassFixture<SuiteUuidFormat.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUuidFormat(Fixture fixture)
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
    public void TestAllUpperCase()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2EB8AA08-AA98-11EA-B4AA-73B441D16380\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllLowerCase()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08-aa98-11ea-b4aa-73b441d16380\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMixedCase()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08-AA98-11ea-B4Aa-73B441D16380\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllZeroesIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"00000000-0000-0000-0000-000000000000\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWrongLength()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08-aa98-11ea-b4aa-73b441d1638\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingSection()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08-aa98-11ea-73b441d16380\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBadCharactersNotHex()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08-aa98-11ea-b4ga-73b441d16380\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoDashes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08aa9811eab4aa73b441d16380\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTooFewDashes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08aa98-11ea-b4aa73b441d16380\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTooManyDashes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8-aa08-aa98-11ea-b4aa73b44-1d16380\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDashesInTheWrongSpot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08aa9811eab4aa73b441d16380----\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestShiftedDashes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa0-8aa98-11e-ab4aa7-3b441d16380\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidVersion4()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"98d80576-482e-427f-8434-7f86890ab222\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidVersion5()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"99c17cbb-656f-564a-940f-1a4568f03487\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHypotheticalVersion6()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"99c17cbb-656f-664a-940f-1a4568f03487\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHypotheticalVersion15()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"99c17cbb-656f-f64a-940f-1a4568f03487\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\uuid.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"uuid\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Uuid",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
