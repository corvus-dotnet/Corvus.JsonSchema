using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft4.MinLength;

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteMinLengthValidation : IClassFixture<SuiteMinLengthValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinLengthValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestLongerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExactLengthIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"fo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTooShortIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"f\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNonStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOneGraphemeIsNotLongEnough()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\uD83D\\uDCA9\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\minLength.json",
                "{\"minLength\": 2}",
                "StandaloneEvaluatorTestSuite.Draft4.MinLength",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
