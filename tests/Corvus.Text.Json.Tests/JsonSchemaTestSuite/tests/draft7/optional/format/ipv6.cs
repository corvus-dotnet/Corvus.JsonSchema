using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.Ipv6;

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreFloats()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("13.7");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreObjects()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreBooleans()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("false");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreNulls()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidIPv6Address()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"::1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIPv6AddressWithOutOfRangeValues()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"12345::\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrailing4HexSymbolsIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"::abef\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrailing5HexSymbolsIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"::abcef\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIPv6AddressWithTooManyComponents()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1:1:1:1:1:1:1:1:1:1:1:1:1:1:1:1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIPv6AddressContainingIllegalCharacters()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"::laptop\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoDigitsIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"::\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeadingColonsIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"::42:ff:1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrailingColonsIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"d6::\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingLeadingOctetIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\":2:3:4:5:6:7:8\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingTrailingOctetIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1:2:3:4:5:6:7:\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingLeadingOctetWithOmittedOctetsLater()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\":2:3:4::8\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleSetOfDoubleColonsInTheMiddleIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1:d6::42\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoSetsOfDoubleColonsIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1::d6::42\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMixedFormatWithTheIpv4SectionAsDecimalOctets()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1::d6:192.168.0.1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMixedFormatWithDoubleColonsBetweenTheSections()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1:2::192.168.0.1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMixedFormatWithIpv4SectionWithOctetOutOfRange()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1::2:192.168.256.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMixedFormatWithIpv4SectionWithAHexOctet()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1::2:192.168.ff.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMixedFormatWithLeadingDoubleColonsIpv4MappedIpv6Address()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"::ffff:192.168.0.1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTripleColonsIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1:2:3:4:5:::8\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test8Octets()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1:2:3:4:5:6:7:8\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInsufficientOctetsWithoutDoubleColons()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1:2:3:4:5:6:7\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoColonsIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIpv4IsNotIpv6()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"127.0.0.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIpv4SegmentMustHave4Octets()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1:2:3:4:1.2.3\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeadingWhitespaceIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"  ::1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrailingWhitespaceIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"::1  \"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNetmaskIsNotAPartOfIpv6Address()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"fe80::/64\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZoneIdIsNotAPartOfIpv6Address()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"fe80::a%eth1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestALongValidIpv6()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1000:1000:1000:1000:1000:1000:255.255.255.255\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestALongInvalidIpv6BelowLengthLimitFirst()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"100:100:100:100:100:100:255.255.255.255.255\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestALongInvalidIpv6BelowLengthLimitSecond()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"100:100:100:100:100:100:100:255.255.255.255\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidNonAscii৪ABengali4()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1:2:3:4:5:6:7:৪\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidNonAscii৪ABengali4InTheIPv4Portion()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1:2::192.16৪.0.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\optional\\format\\ipv6.json",
                "{ \"format\": \"ipv6\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.Ipv6",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
