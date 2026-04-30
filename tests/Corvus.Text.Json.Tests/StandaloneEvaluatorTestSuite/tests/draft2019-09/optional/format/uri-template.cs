using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.UriTemplate;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteFormatUriTemplate : IClassFixture<SuiteFormatUriTemplate.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteFormatUriTemplate(Fixture fixture)
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
    public void TestAValidUriTemplate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com/dictionary/{term:1}/{term}\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidUriTemplate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com/dictionary/{term:1}/{term\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidUriTemplateWithoutVariables()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com/dictionary\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidRelativeUriTemplate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"dictionary/{term:1}/{term}\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\uri-template.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"uri-template\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.UriTemplate",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
