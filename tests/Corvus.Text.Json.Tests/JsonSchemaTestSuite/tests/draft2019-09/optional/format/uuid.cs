using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Optional.Format.Uuid;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteUuidFormat : IClassFixture<SuiteUuidFormat.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUuidFormat(Fixture fixture)
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
    public void TestAllUpperCase()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2EB8AA08-AA98-11EA-B4AA-73B441D16380\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllLowerCase()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2eb8aa08-aa98-11ea-b4aa-73b441d16380\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMixedCase()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2eb8aa08-AA98-11ea-B4Aa-73B441D16380\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllZeroesIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"00000000-0000-0000-0000-000000000000\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWrongLength()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2eb8aa08-aa98-11ea-b4aa-73b441d1638\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingSection()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2eb8aa08-aa98-11ea-73b441d16380\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBadCharactersNotHex()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2eb8aa08-aa98-11ea-b4ga-73b441d16380\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoDashes()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2eb8aa08aa9811eab4aa73b441d16380\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTooFewDashes()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2eb8aa08aa98-11ea-b4aa73b441d16380\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTooManyDashes()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2eb8-aa08-aa98-11ea-b4aa73b44-1d16380\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDashesInTheWrongSpot()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2eb8aa08aa9811eab4aa73b441d16380----\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestShiftedDashes()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2eb8aa0-8aa98-11e-ab4aa7-3b441d16380\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidVersion4()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"98d80576-482e-427f-8434-7f86890ab222\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidVersion5()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"99c17cbb-656f-564a-940f-1a4568f03487\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHypotheticalVersion6()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"99c17cbb-656f-664a-940f-1a4568f03487\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHypotheticalVersion15()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"99c17cbb-656f-f64a-940f-1a4568f03487\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\format\\uuid.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"uuid\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.Format.Uuid",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
