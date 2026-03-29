using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.AdditionalProperties;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteAdditionalPropertiesBeingFalseDoesNotAllowOtherProperties : IClassFixture<SuiteAdditionalPropertiesBeingFalseDoesNotAllowOtherProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesBeingFalseDoesNotAllowOtherProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNoAdditionalPropertiesIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnAdditionalPropertyIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\" : 1, \"bar\" : 2, \"quux\" : \"boom\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 2, 3]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foobarbaz\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresOtherNonObjects()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPatternPropertiesAreNotAdditionalProperties()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\":1, \"vroom\": 2}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\"foo\": {}, \"bar\": {}},\r\n            \"patternProperties\": { \"^v\": {} },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalProperties",
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
public class SuiteNonAsciiPatternWithAdditionalProperties : IClassFixture<SuiteNonAsciiPatternWithAdditionalProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNonAsciiPatternWithAdditionalProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchingThePatternIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"ármányos\": 2}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotMatchingThePatternIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"élmény\": 2}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"patternProperties\": {\"^á\": {}},\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalProperties",
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
public class SuiteAdditionalPropertiesWithSchema : IClassFixture<SuiteAdditionalPropertiesWithSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesWithSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNoAdditionalPropertiesIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnAdditionalValidPropertyIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\" : 1, \"bar\" : 2, \"quux\" : true}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnAdditionalInvalidPropertyIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\" : 1, \"bar\" : 2, \"quux\" : 12}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\"foo\": {}, \"bar\": {}},\r\n            \"additionalProperties\": {\"type\": \"boolean\"}\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalProperties",
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
public class SuiteAdditionalPropertiesCanExistByItself : IClassFixture<SuiteAdditionalPropertiesCanExistByItself.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesCanExistByItself(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnAdditionalValidPropertyIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\" : true}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnAdditionalInvalidPropertyIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\" : 1}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"additionalProperties\": {\"type\": \"boolean\"}\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalProperties",
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
public class SuiteAdditionalPropertiesAreAllowedByDefault : IClassFixture<SuiteAdditionalPropertiesAreAllowedByDefault.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesAreAllowedByDefault(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAdditionalPropertiesAreAllowed()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2, \"quux\": true}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\"foo\": {}, \"bar\": {}}\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalProperties",
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
public class SuiteAdditionalPropertiesDoesNotLookInApplicators : IClassFixture<SuiteAdditionalPropertiesDoesNotLookInApplicators.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesDoesNotLookInApplicators(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPropertiesDefinedInAllOfAreNotExamined()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": true}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {\"properties\": {\"foo\": {}}}\r\n            ],\r\n            \"additionalProperties\": {\"type\": \"boolean\"}\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalProperties",
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
public class SuiteAdditionalPropertiesWithNullValuedInstanceProperties : IClassFixture<SuiteAdditionalPropertiesWithNullValuedInstanceProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesWithNullValuedInstanceProperties(Fixture fixture)
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
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"additionalProperties\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalProperties",
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
public class SuiteAdditionalPropertiesWithPropertyNames : IClassFixture<SuiteAdditionalPropertiesWithPropertyNames.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAdditionalPropertiesWithPropertyNames(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidAgainstBothKeywords()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"apple\": 4 }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidAgainstPropertyNamesButNotAdditionalProperties()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"fig\": 2, \"pear\": \"available\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"propertyNames\": {\r\n                \"maxLength\": 5\r\n            },\r\n            \"additionalProperties\": {\r\n                \"type\": \"number\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalProperties",
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
public class SuiteDependentSchemasWithAdditionalProperties : IClassFixture<SuiteDependentSchemasWithAdditionalProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDependentSchemasWithAdditionalProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAdditionalPropertiesDoesnTConsiderDependentSchemas()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAdditionalPropertiesCanTSeeBar()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": \"\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAdditionalPropertiesCanTSeeBarEvenWhenFoo2IsPresent()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo2\": \"\", \"bar\": \"\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\additionalProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\"foo2\": {}},\r\n            \"dependentSchemas\": {\r\n                \"foo\" : {},\r\n                \"foo2\": {\r\n                    \"properties\": {\r\n                        \"bar\":{}\r\n                    }\r\n                }\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft201909.AdditionalProperties",
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
