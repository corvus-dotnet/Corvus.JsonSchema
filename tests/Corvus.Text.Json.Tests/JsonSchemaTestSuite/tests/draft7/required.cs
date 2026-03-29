using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Required;

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteRequiredValidation : IClassFixture<SuiteRequiredValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRequiredValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPresentRequiredPropertyIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonPresentRequiredPropertyIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": 1}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresOtherNonObjects()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNull()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresBoolean()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\required.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {},\r\n                \"bar\": {}\r\n            },\r\n            \"required\": [\"foo\"]\r\n        }",
                "JsonSchemaTestSuite.Draft7.Required",
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
public class SuiteRequiredDefaultValidation : IClassFixture<SuiteRequiredDefaultValidation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRequiredDefaultValidation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNotRequiredByDefault()
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
                "tests\\draft7\\required.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft7.Required",
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
public class SuiteRequiredWithEmptyArray : IClassFixture<SuiteRequiredWithEmptyArray.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRequiredWithEmptyArray(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPropertyNotRequired()
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
                "tests\\draft7\\required.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {}\r\n            },\r\n            \"required\": []\r\n        }",
                "JsonSchemaTestSuite.Draft7.Required",
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
public class SuiteRequiredWithEscapedCharacters : IClassFixture<SuiteRequiredWithEscapedCharacters.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRequiredWithEscapedCharacters(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestObjectWithAllPropertiesPresentIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\nbar\": 1,\r\n                    \"foo\\\"bar\": 1,\r\n                    \"foo\\\\bar\": 1,\r\n                    \"foo\\rbar\": 1,\r\n                    \"foo\\tbar\": 1,\r\n                    \"foo\\fbar\": 1\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestObjectWithSomePropertiesMissingIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\nbar\": \"1\",\r\n                    \"foo\\\"bar\": \"1\"\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\required.json",
                "{\r\n            \"required\": [\r\n                \"foo\\nbar\",\r\n                \"foo\\\"bar\",\r\n                \"foo\\\\bar\",\r\n                \"foo\\rbar\",\r\n                \"foo\\tbar\",\r\n                \"foo\\fbar\"\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft7.Required",
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
public class SuiteRequiredPropertiesWhoseNamesAreJavascriptObjectPropertyNames : IClassFixture<SuiteRequiredPropertiesWhoseNamesAreJavascriptObjectPropertyNames.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRequiredPropertiesWhoseNamesAreJavascriptObjectPropertyNames(Fixture fixture)
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
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestProtoPresent()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"__proto__\": \"foo\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestToStringPresent()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"toString\": { \"length\": 37 } }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestConstructorPresent()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"constructor\": { \"length\": 37 } }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllPresent()
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
                "tests\\draft7\\required.json",
                "{ \"required\": [\"__proto__\", \"toString\", \"constructor\"] }",
                "JsonSchemaTestSuite.Draft7.Required",
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
