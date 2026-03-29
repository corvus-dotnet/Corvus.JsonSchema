using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.Email;

[Trait("JsonSchemaTestSuite", "Draft7")]
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
                "tests\\draft7\\optional\\format\\email.json",
                "{ \"format\": \"email\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.Email",
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
