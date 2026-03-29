using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft6.Minimum;

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteMinimumValidation : IClassFixture<SuiteMinimumValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinimumValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAboveTheMinimumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2.6");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBoundaryPointIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBelowTheMinimumIsInvalid()
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
                "tests\\draft6\\minimum.json",
                "{\"minimum\": 1.1}",
                "StandaloneEvaluatorTestSuite.Draft6.Minimum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteMinimumValidationWithSignedInteger : IClassFixture<SuiteMinimumValidationWithSignedInteger.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinimumValidationWithSignedInteger(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNegativeAboveTheMinimumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPositiveAboveTheMinimumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBoundaryPointIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-2");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBoundaryPointWithFloatIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-2.0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFloatBelowTheMinimumIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-2.0001");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIntBelowTheMinimumIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-3");
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
                "tests\\draft6\\minimum.json",
                "{\"minimum\": -2}",
                "StandaloneEvaluatorTestSuite.Draft6.Minimum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
