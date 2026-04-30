using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft4.Properties;

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuiteObjectPropertiesValidation : IClassFixture<SuiteObjectPropertiesValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteObjectPropertiesValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBothPropertiesPresentAndValidIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": \"baz\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOnePropertyInvalidIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": {}}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBothPropertiesInvalidIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": [], \"bar\": {}}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDoesnTInvalidateOtherProperties()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"quux\": []}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresOtherNonObjects()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\properties.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {\"type\": \"integer\"},\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuitePropertiesPatternPropertiesAdditionalPropertiesInteraction : IClassFixture<SuitePropertiesPatternPropertiesAdditionalPropertiesInteraction.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertiesPatternPropertiesAdditionalPropertiesInteraction(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPropertyValidatesProperty()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": [1, 2]}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPropertyInvalidatesProperty()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": [1, 2, 3, 4]}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPatternPropertyInvalidatesProperty()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": []}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPatternPropertyValidatesNonproperty()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"fxo\": [1, 2]}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPatternPropertyInvalidatesNonproperty()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"fxo\": []}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAdditionalPropertyIgnoresProperty()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": []}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAdditionalPropertyValidatesOthers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"quux\": 3}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAdditionalPropertyInvalidatesOthers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"quux\": \"foo\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\properties.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {\"type\": \"array\", \"maxItems\": 3},\r\n                \"bar\": {\"type\": \"array\"}\r\n            },\r\n            \"patternProperties\": {\"f.o\": {\"minItems\": 2}},\r\n            \"additionalProperties\": {\"type\": \"integer\"}\r\n        }",
                "JsonSchemaTestSuite.Draft4.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuitePropertiesWithEscapedCharacters : IClassFixture<SuitePropertiesWithEscapedCharacters.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertiesWithEscapedCharacters(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestObjectWithAllNumbersIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\nbar\": 1,\r\n                    \"foo\\\"bar\": 1,\r\n                    \"foo\\\\bar\": 1,\r\n                    \"foo\\rbar\": 1,\r\n                    \"foo\\tbar\": 1,\r\n                    \"foo\\fbar\": 1\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestObjectWithStringsIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\nbar\": \"1\",\r\n                    \"foo\\\"bar\": \"1\",\r\n                    \"foo\\\\bar\": \"1\",\r\n                    \"foo\\rbar\": \"1\",\r\n                    \"foo\\tbar\": \"1\",\r\n                    \"foo\\fbar\": \"1\"\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\properties.json",
                "{\r\n            \"properties\": {\r\n                \"foo\\nbar\": {\"type\": \"number\"},\r\n                \"foo\\\"bar\": {\"type\": \"number\"},\r\n                \"foo\\\\bar\": {\"type\": \"number\"},\r\n                \"foo\\rbar\": {\"type\": \"number\"},\r\n                \"foo\\tbar\": {\"type\": \"number\"},\r\n                \"foo\\fbar\": {\"type\": \"number\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuitePropertiesWithNullValuedInstanceProperties : IClassFixture<SuitePropertiesWithNullValuedInstanceProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertiesWithNullValuedInstanceProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllowsNullValues()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": null}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\properties.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {\"type\": \"null\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuitePropertiesWhoseNamesAreJavascriptObjectPropertyNames : IClassFixture<SuitePropertiesWhoseNamesAreJavascriptObjectPropertyNames.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertiesWhoseNamesAreJavascriptObjectPropertyNames(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresOtherNonObjects()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoneOfThePropertiesMentioned()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestProtoNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"__proto__\": \"foo\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestToStringNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"toString\": { \"length\": 37 } }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestConstructorNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"constructor\": { \"length\": 37 } }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllPresentAndValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \r\n                    \"__proto__\": 12,\r\n                    \"toString\": { \"length\": \"foo\" },\r\n                    \"constructor\": 37\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\properties.json",
                "{\r\n            \"properties\": {\r\n                \"__proto__\": {\"type\": \"number\"},\r\n                \"toString\": {\r\n                    \"properties\": { \"length\": { \"type\": \"string\" } }\r\n                },\r\n                \"constructor\": {\"type\": \"number\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
