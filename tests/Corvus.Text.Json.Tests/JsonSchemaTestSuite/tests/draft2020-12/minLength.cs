using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.MinLength;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteMinLengthValidation : IClassFixture<SuiteMinLengthValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinLengthValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestLongerIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExactLengthIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"fo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTooShortIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"f\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonStrings()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOneGraphemeIsNotLongEnough()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\uD83D\\uDCA9\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\minLength.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"minLength\": 2\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MinLength",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteMinLengthValidationWithADecimal : IClassFixture<SuiteMinLengthValidationWithADecimal.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinLengthValidationWithADecimal(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestLongerIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTooShortIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"f\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\minLength.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"minLength\": 2.0\r\n        }",
                "JsonSchemaTestSuite.Draft202012.MinLength",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
