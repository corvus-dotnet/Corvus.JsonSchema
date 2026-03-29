using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Default;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteInvalidTypeForDefault : IClassFixture<SuiteInvalidTypeForDefault.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteInvalidTypeForDefault(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWhenPropertyIsSpecified()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 13}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestStillValidWhenTheInvalidDefaultIsUsed()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\default.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"type\": \"integer\",\r\n                    \"default\": []\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Default",
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
public class SuiteInvalidStringValueForDefault : IClassFixture<SuiteInvalidStringValueForDefault.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteInvalidStringValueForDefault(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidWhenPropertyIsSpecified()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": \"good\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestStillValidWhenTheInvalidDefaultIsUsed()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\default.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"bar\": {\r\n                    \"type\": \"string\",\r\n                    \"minLength\": 4,\r\n                    \"default\": \"bad\"\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Default",
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
public class SuiteTheDefaultKeywordDoesNotDoAnythingIfThePropertyIsMissing : IClassFixture<SuiteTheDefaultKeywordDoesNotDoAnythingIfThePropertyIsMissing.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteTheDefaultKeywordDoesNotDoAnythingIfThePropertyIsMissing(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnExplicitPropertyValueIsCheckedAgainstMaximumPassing()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"alpha\": 1 }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnExplicitPropertyValueIsCheckedAgainstMaximumFailing()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"alpha\": 5 }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingPropertiesAreNotFilledInWithTheDefault()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\default.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"alpha\": {\r\n                    \"type\": \"number\",\r\n                    \"maximum\": 3,\r\n                    \"default\": 5\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Default",
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
