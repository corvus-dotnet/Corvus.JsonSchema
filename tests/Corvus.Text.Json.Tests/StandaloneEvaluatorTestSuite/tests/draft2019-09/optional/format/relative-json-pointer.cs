using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.RelativeJsonPointer;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteValidationOfRelativeJsonPointersRjp : IClassFixture<SuiteValidationOfRelativeJsonPointersRjp.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfRelativeJsonPointersRjp(Fixture fixture)
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
    public void TestAValidUpwardsRjp()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidDownwardsRjp()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0/foo/bar\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidUpAndThenDownRjpWithArrayIndex()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2/0/baz/1/zip\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidRjpTakingTheMemberOrIndexName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0#\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidRjpThatIsAValidJsonPointer()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/bar\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNegativePrefix()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-1/foo/bar\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExplicitPositivePrefix()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"+1/foo/bar\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIsNotAValidJsonPointer()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0##\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroCannotBeFollowedByOtherDigitsPlusJsonPointer()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"01/a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroCannotBeFollowedByOtherDigitsPlusOctothorpe()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"01#\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMultiDigitIntegerPrefix()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"120/foo/bar\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\relative-json-pointer.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"relative-json-pointer\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.RelativeJsonPointer",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
