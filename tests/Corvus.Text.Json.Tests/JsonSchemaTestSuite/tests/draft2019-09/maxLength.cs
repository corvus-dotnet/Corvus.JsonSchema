using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.MaxLength;

[Trait("JsonSchemaTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\maxLength.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"maxLength\": 2\r\n        }",
                "JsonSchemaTestSuite.Draft201909.MaxLength",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft201909")]
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
                "tests\\draft2019-09\\maxLength.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"maxLength\": 2.0\r\n        }",
                "JsonSchemaTestSuite.Draft201909.MaxLength",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
