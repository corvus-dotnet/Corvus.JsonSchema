using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft6.MinItems;

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteMinItemsValidation : IClassFixture<SuiteMinItemsValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinItemsValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestLongerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExactLengthIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTooShortIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNonArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\minItems.json",
                "{\"minItems\": 1}",
                "StandaloneEvaluatorTestSuite.Draft6.MinItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteMinItemsValidationWithADecimal : IClassFixture<SuiteMinItemsValidationWithADecimal.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinItemsValidationWithADecimal(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestLongerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTooShortIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\minItems.json",
                "{\"minItems\": 1.0}",
                "StandaloneEvaluatorTestSuite.Draft6.MinItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
