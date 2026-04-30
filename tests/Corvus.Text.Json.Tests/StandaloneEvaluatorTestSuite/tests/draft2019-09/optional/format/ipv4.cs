using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Ipv4;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteValidationOfIpAddresses : IClassFixture<SuiteValidationOfIpAddresses.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfIpAddresses(Fixture fixture)
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
    public void TestAValidIpAddress()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIpAddressWithTooManyComponents()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"127.0.0.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIpAddressWithOutOfRangeValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"256.256.256.256\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIpAddressWithout4Components()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"127.0\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIpAddressAsAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0x7f000001\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIpAddressAsAnIntegerDecimal()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2130706433\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidNonAscii২ABengali2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1২7.0.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNetmaskIsNotAPartOfIpv4Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.1.0/24\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLeadingWhitespaceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\" 192.168.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailingWhitespaceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1 \"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailingNewlineIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1\\n\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHexadecimalNotationIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0x7f.0.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestOctalNotationExplicitIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0o10.0.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyPartDoubleDotIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168..1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLeadingDotIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".192.168.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailingDotIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1.\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMinimumValidIPv4Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0.0.0.0\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMaximumValidIPv4Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"255.255.255.255\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPlusSignIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"+1.2.3.4\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNegativeSignIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-1.2.3.4\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExponentialNotationIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1e2.0.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAlphaCharactersAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.a.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInternalWhitespaceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192. 168.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTabCharacterIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1\\t\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithPortNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1:80\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleOctetOutOfRangeInLastPosition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.256\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\ipv4.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"ipv4\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Ipv4",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
