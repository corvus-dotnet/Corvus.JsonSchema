using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Optional.Format.Email;

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
    public void TestAValidEMailAddress()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"joe.bloggs@example.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidEMailAddress()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2962\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTildeInLocalPartIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"te~st@example.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTildeBeforeLocalPartIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"~test@example.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTildeAfterLocalPartIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"test~@example.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAQuotedStringWithASpaceInTheLocalPartIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\\"joe bloggs\\\"@example.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAQuotedStringWithADoubleDotInTheLocalPartIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\\"joe..bloggs\\\"@example.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAQuotedStringWithAInTheLocalPartIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\\"joe@bloggs\\\"@example.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIPv4AddressLiteralAfterTheIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"joe.bloggs@[127.0.0.1]\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIPv6AddressLiteralAfterTheIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"joe.bloggs@[IPv6:::1]\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDotBeforeLocalPartIsNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\".test@example.com\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDotAfterLocalPartIsNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"test.@example.com\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoSeparatedDotsInsideLocalPartAreValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"te.s.t@example.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoSubsequentDotsInsideLocalPartAreNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"te..st@example.com\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidDomain()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"joe.bloggs@invalid=domain.com\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidIPv4AddressLiteral()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"joe.bloggs@[127.0.0.300]\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoEmailAddressesIsNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"user1@oceania.org, user2@oceania.org\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFullFromHeaderIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\\"Winston Smith\\\" <winston.smith@recdep.minitrue> (Records Department)\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format\\email.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"email\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.Email",
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
