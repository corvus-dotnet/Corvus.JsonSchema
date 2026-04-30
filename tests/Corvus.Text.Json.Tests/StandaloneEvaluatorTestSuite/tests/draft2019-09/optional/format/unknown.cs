using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Unknown;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnknownFormat : IClassFixture<SuiteUnknownFormat.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnknownFormat(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestUnknownFormatsIgnoreIntegers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnknownFormatsIgnoreFloats()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("13.7");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnknownFormatsIgnoreObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnknownFormatsIgnoreArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnknownFormatsIgnoreBooleans()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnknownFormatsIgnoreNulls()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnknownFormatsIgnoreStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"string\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\unknown.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"unknown\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Unknown",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
