using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.IriReference;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteValidationOfIriReferences : IClassFixture<SuiteValidationOfIriReferences.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfIriReferences(Fixture fixture)
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
    public void TestAValidIri()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.ßår/?∂éœ=πîx#πîüx\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidProtocolRelativeIriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"//ƒøø.ßår/?∂éœ=πîx#πîüx\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidRelativeIriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/âππ\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidIriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\\\\\WINDOWS\\\\filëßåré\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidIriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"âππ\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidIriFragment()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"#ƒrägmênt\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidIriFragment()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"#ƒräg\\\\mênt\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format\\iri-reference.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"iri-reference\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.IriReference",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
