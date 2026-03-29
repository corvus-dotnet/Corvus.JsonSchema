using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Optional.Format.Ipv4;

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
    public void TestInvalidLeadingZeroesAsTheyAreTreatedAsOctals()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"087.10.0.1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValueWithoutLeadingZeroIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"87.10.0.1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
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

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format\\ipv4.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"ipv4\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.Ipv4",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
