using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.MaxLength;

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteMaxLengthValidation : IClassFixture<SuiteMaxLengthValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxLengthValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestShorterIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"f\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExactLengthIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"fo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTooLongIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonStrings()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("100");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoGraphemesIsLongEnough()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\uD83D\\uDCA9\\uD83D\\uDCA9\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\maxLength.json",
                "{\"maxLength\": 2}",
                "JsonSchemaTestSuite.Draft7.MaxLength",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteMaxLengthValidationWithADecimal : IClassFixture<SuiteMaxLengthValidationWithADecimal.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMaxLengthValidationWithADecimal(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestShorterIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"f\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTooLongIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\maxLength.json",
                "{\"maxLength\": 2.0}",
                "JsonSchemaTestSuite.Draft7.MaxLength",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
