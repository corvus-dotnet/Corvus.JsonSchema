using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Email;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteValidationOfEMailAddresses : IClassFixture<SuiteValidationOfEMailAddresses.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfEMailAddresses(Fixture fixture)
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
    public void TestAValidEMailAddress()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidEMailAddress()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2962\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTildeInLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"te~st@example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTildeBeforeLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"~test@example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTildeAfterLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test~@example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAQuotedStringWithASpaceInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"joe bloggs\\\"@example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAQuotedStringWithADoubleDotInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"joe..bloggs\\\"@example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAQuotedStringWithAInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"joe@bloggs\\\"@example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIPv4AddressLiteralAfterTheIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@[127.0.0.1]\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIPv6AddressLiteralAfterTheIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@[IPv6:::1]\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDotBeforeLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".test@example.com\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDotAfterLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test.@example.com\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTwoSeparatedDotsInsideLocalPartAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"te.s.t@example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTwoSubsequentDotsInsideLocalPartAreNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"te..st@example.com\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidDomain()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@invalid=domain.com\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidIPv4AddressLiteral()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@[127.0.0.300]\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTwoEmailAddressesIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"user1@oceania.org, user2@oceania.org\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFullFromHeaderIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"Winston Smith\\\" <winston.smith@recdep.minitrue> (Records Department)\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format\\email.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"email\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Email",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
