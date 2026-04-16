using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft4.Optional.NonBmpRegex;

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteProperUtf16SurrogatePairHandlingPattern : IClassFixture<SuiteProperUtf16SurrogatePairHandlingPattern.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteProperUtf16SurrogatePairHandlingPattern(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesEmpty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatchesSingle()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"🐲\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatchesTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"🐲🐲\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDoesnTMatchOne()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"🐉\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDoesnTMatchTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"🐉🐉\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDoesnTMatchOneAscii()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"D\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDoesnTMatchTwoAscii()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"DD\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\optional\\non-bmp-regex.json",
                "{ \"pattern\": \"^🐲*$\" }",
                "StandaloneEvaluatorTestSuite.Draft4.Optional.NonBmpRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteProperUtf16SurrogatePairHandlingPatternProperties : IClassFixture<SuiteProperUtf16SurrogatePairHandlingPatternProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteProperUtf16SurrogatePairHandlingPatternProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesEmpty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatchesSingle()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"🐲\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatchesTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"🐲🐲\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDoesnTMatchOne()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"🐲\": \"hello\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDoesnTMatchTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"🐲🐲\": \"hello\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\optional\\non-bmp-regex.json",
                "{\r\n            \"patternProperties\": {\r\n                \"^🐲*$\": {\r\n                    \"type\": \"integer\"\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Optional.NonBmpRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
