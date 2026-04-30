using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft7.Pattern;

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuitePatternValidation : IClassFixture<SuitePatternValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAMatchingPatternIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"aaa\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestANonMatchingPatternIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abc\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresBooleans()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresIntegers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("123");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresFloats()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\pattern.json",
                "{\"pattern\": \"^a*$\"}",
                "StandaloneEvaluatorTestSuite.Draft7.Pattern",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuitePatternIsNotAnchored : IClassFixture<SuitePatternIsNotAnchored.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternIsNotAnchored(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesASubstring()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xxaayy\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\pattern.json",
                "{\"pattern\": \"a+\"}",
                "StandaloneEvaluatorTestSuite.Draft7.Pattern",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
