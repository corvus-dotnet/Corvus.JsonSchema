using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Minimum;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteMinimumValidation : IClassFixture<SuiteMinimumValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinimumValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAboveTheMinimumIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("2.6");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBoundaryPointIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1.1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBelowTheMinimumIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0.6");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonNumbers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"x\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\minimum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"minimum\": 1.1\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Minimum",
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
public class SuiteMinimumValidationWithSignedInteger : IClassFixture<SuiteMinimumValidationWithSignedInteger.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMinimumValidationWithSignedInteger(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNegativeAboveTheMinimumIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPositiveAboveTheMinimumIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBoundaryPointIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-2");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBoundaryPointWithFloatIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-2.0");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFloatBelowTheMinimumIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-2.0001");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIntBelowTheMinimumIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("-3");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonNumbers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"x\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\minimum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"minimum\": -2\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Minimum",
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
