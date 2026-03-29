using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Ipv6;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteValidationOfIPv6Addresses : IClassFixture<SuiteValidationOfIPv6Addresses.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfIPv6Addresses(Fixture fixture)
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
    public void TestAValidIPv6Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIPv6AddressWithOutOfRangeValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"12345::\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailing4HexSymbolsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::abef\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailing5HexSymbolsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::abcef\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIPv6AddressWithTooManyComponents()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:1:1:1:1:1:1:1:1:1:1:1:1:1:1:1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIPv6AddressContainingIllegalCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::laptop\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoDigitsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLeadingColonsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::42:ff:1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailingColonsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"d6::\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingLeadingOctetIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\":2:3:4:5:6:7:8\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingTrailingOctetIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2:3:4:5:6:7:\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMissingLeadingOctetWithOmittedOctetsLater()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\":2:3:4::8\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleSetOfDoubleColonsInTheMiddleIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:d6::42\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTwoSetsOfDoubleColonsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1::d6::42\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMixedFormatWithTheIpv4SectionAsDecimalOctets()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1::d6:192.168.0.1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMixedFormatWithDoubleColonsBetweenTheSections()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2::192.168.0.1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMixedFormatWithIpv4SectionWithOctetOutOfRange()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1::2:192.168.256.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMixedFormatWithIpv4SectionWithAHexOctet()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1::2:192.168.ff.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMixedFormatWithLeadingDoubleColonsIpv4MappedIpv6Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::ffff:192.168.0.1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTripleColonsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2:3:4:5:::8\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void Test8Octets()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2:3:4:5:6:7:8\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInsufficientOctetsWithoutDoubleColons()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2:3:4:5:6:7\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoColonsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIpv4IsNotIpv6()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"127.0.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIpv4SegmentMustHave4Octets()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2:3:4:1.2.3\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLeadingWhitespaceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"  ::1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailingWhitespaceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::1  \"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNetmaskIsNotAPartOfIpv6Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"fe80::/64\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZoneIdIsNotAPartOfIpv6Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"fe80::a%eth1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestALongValidIpv6()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1000:1000:1000:1000:1000:1000:255.255.255.255\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestALongInvalidIpv6BelowLengthLimitFirst()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"100:100:100:100:100:100:255.255.255.255.255\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestALongInvalidIpv6BelowLengthLimitSecond()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"100:100:100:100:100:100:100:255.255.255.255\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidNonAscii৪ABengali4()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2:3:4:5:6:7:৪\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidNonAscii৪ABengali4InTheIPv4Portion()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2::192.16৪.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\ipv6.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"ipv6\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Ipv6",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
