using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.Ipv4;

[Trait("JsonSchemaTestSuite", "Draft7")]
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
    public void TestAValidIpAddress()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"192.168.0.1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIpAddressWithTooManyComponents()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"127.0.0.0.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIpAddressWithOutOfRangeValues()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"256.256.256.256\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIpAddressWithout4Components()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"127.0\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIpAddressAsAnInteger()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"0x7f000001\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIpAddressAsAnIntegerDecimal()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2130706433\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidNonAscii২ABengali2()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1২7.0.0.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNetmaskIsNotAPartOfIpv4Address()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"192.168.1.0/24\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeadingWhitespaceIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\" 192.168.0.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrailingWhitespaceIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"192.168.0.1 \"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrailingNewlineIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"192.168.0.1\\n\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHexadecimalNotationIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"0x7f.0.0.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOctalNotationExplicitIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"0o10.0.0.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmptyPartDoubleDotIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"192.168..1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeadingDotIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\".192.168.0.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrailingDotIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"192.168.0.1.\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMinimumValidIPv4Address()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"0.0.0.0\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMaximumValidIPv4Address()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"255.255.255.255\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmptyStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPlusSignIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"+1.2.3.4\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNegativeSignIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"-1.2.3.4\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExponentialNotationIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1e2.0.0.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAlphaCharactersAreInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"192.168.a.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInternalWhitespaceIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"192. 168.0.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTabCharacterIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"192.168.0.1\\t\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithPortNumberIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"192.168.0.1:80\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleOctetOutOfRangeInLastPosition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"192.168.0.256\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\optional\\format\\ipv4.json",
                "{ \"format\": \"ipv4\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.Ipv4",
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
