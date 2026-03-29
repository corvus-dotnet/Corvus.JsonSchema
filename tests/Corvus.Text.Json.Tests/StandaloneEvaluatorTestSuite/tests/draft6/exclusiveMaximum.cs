using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft6.ExclusiveMaximum;

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteExclusiveMaximumValidation : IClassFixture<SuiteExclusiveMaximumValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteExclusiveMaximumValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBelowTheExclusiveMaximumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2.2");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBoundaryPointIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3.0");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAboveTheExclusiveMaximumIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3.5");
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
                "tests\\draft6\\exclusiveMaximum.json",
                "{\r\n            \"exclusiveMaximum\": 3.0\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.ExclusiveMaximum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
