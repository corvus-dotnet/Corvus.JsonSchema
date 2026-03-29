using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Iri;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteValidationOfIrIs : IClassFixture<SuiteValidationOfIrIs.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfIrIs(Fixture fixture)
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
    public void TestAValidIriWithAnchorTag()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.ßår/?∂éœ=πîx#πîüx\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidIriWithAnchorTagAndParentheses()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.com/blah_(wîkïpédiå)_blah#ßité-1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidIriWithUrlEncodedStuff()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.ßår/?q=Test%20URL-encoded%20stuff\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidIriWithManySpecialCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://-.~_!$&'()*+,;=:%40:80%2f::::::@example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidIriBasedOnIPv6()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://[2001:0db8:85a3:0000:0000:8a2e:0370:7334]\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidIriBasedOnIPv6()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://2001:0db8:85a3:0000:0000:8a2e:0370:7334\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidRelativeIriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/abc\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidIri()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\\\\\WINDOWS\\\\filëßåré\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidIriThoughValidIriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"âππ\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\iri.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"iri\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Iri",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
