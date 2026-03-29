using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft7.ExclusiveMinimum;

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
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
                "tests\\draft7\\exclusiveMinimum.json",
                "{\r\n            \"exclusiveMinimum\": 1.1\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.ExclusiveMinimum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
