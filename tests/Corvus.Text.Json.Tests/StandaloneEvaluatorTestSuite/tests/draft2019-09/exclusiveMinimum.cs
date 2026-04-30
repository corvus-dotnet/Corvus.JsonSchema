using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.ExclusiveMinimum;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteExclusiveMinimumValidation : IClassFixture<SuiteExclusiveMinimumValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteExclusiveMinimumValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAboveTheExclusiveMinimumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.2");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBoundaryPointIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBelowTheExclusiveMinimumIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.6");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNonNumbers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"x\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\exclusiveMinimum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"exclusiveMinimum\": 1.1\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.ExclusiveMinimum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
