using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Maximum;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMaximumValidation : IClassFixture<SuiteMaximumValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaximumValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBelowTheMaximumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2.6");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBoundaryPointIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3.0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAboveTheMaximumIsInvalid()
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
                "tests\\draft2020-12\\maximum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"maximum\": 3.0\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Maximum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteMaximumValidationWithUnsignedInteger : IClassFixture<SuiteMaximumValidationWithUnsignedInteger.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaximumValidationWithUnsignedInteger(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBelowTheMaximumIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("299.97");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBoundaryPointIntegerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("300");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBoundaryPointFloatIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("300.00");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAboveTheMaximumIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("300.5");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\maximum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"maximum\": 300\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Maximum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
