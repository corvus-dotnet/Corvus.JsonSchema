using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft4.Optional.Format.Hostname;

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteValidationOfHostNames : IClassFixture<SuiteValidationOfHostNames.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfHostNames(Fixture fixture)
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
    public void TestAValidHostName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"www.example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidPunycodedIdnHostname()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--4gbwdl.xn--wgbh1c\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAHostNameStartingWithAnIllegalCharacter()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-a-host-name-that-starts-with--\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAHostNameContainingIllegalCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"not_a_valid_host_name\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAHostNameWithAComponentTooLong()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a-vvvvvvvvvvvvvvvveeeeeeeeeeeeeeeerrrrrrrrrrrrrrrryyyyyyyyyyyyyyyy-long-host-name-component\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStartsWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-hostname\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEndsWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostname-\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStartsWithUnderscore()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"_hostname\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEndsWithUnderscore()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostname_\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestContainsUnderscore()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"host_name\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMaximumLabelLength()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExceedsMaximumLabelLength()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl.com\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleLabel()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostname\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleLabelWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"host-name\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleLabelWithDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"h0stn4me\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleLabelEndingWithDigit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostnam3\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLeadingDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".example\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailingDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example.\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIdnLabelSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example\\uff0ecom\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\optional\\format\\hostname.json",
                "{ \"format\": \"hostname\" }",
                "StandaloneEvaluatorTestSuite.Draft4.Optional.Format.Hostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
