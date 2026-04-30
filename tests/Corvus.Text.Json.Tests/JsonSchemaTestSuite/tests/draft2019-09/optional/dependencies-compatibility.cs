using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Optional.DependenciesCompatibility;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteSingleDependency : IClassFixture<SuiteSingleDependency.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSingleDependency(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNeither()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNondependant()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithDependency()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingDependency()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": 2}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foobar\"");
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
                "tests\\draft2019-09\\optional\\dependencies-compatibility.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"dependencies\": {\"bar\": [\"foo\"]}\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.DependenciesCompatibility",
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
public class SuiteEmptyDependents : IClassFixture<SuiteEmptyDependents.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEmptyDependents(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyObject()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestObjectWithOneProperty()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": 2}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonObjectIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\dependencies-compatibility.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"dependencies\": {\"bar\": []}\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.DependenciesCompatibility",
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
public class SuiteMultipleDependentsRequired : IClassFixture<SuiteMultipleDependentsRequired.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMultipleDependentsRequired(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNeither()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNondependants()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWithDependencies()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2, \"quux\": 3}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingDependency()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1, \"quux\": 2}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingOtherDependency()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": 1, \"quux\": 2}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingBothDependencies()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"quux\": 1}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\dependencies-compatibility.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"dependencies\": {\"quux\": [\"foo\", \"bar\"]}\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.DependenciesCompatibility",
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
public class SuiteDependenciesWithEscapedCharacters : IClassFixture<SuiteDependenciesWithEscapedCharacters.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDependenciesWithEscapedCharacters(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestCrlf()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\nbar\": 1,\r\n                    \"foo\\rbar\": 2\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestQuotedQuotes()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo'bar\": 1,\r\n                    \"foo\\\"bar\": 2\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestCrlfMissingDependent()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\nbar\": 1,\r\n                    \"foo\": 2\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestQuotedQuotesMissingDependent()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\\"bar\": 2\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\dependencies-compatibility.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"dependencies\": {\r\n                \"foo\\nbar\": [\"foo\\rbar\"],\r\n                \"foo\\\"bar\": [\"foo'bar\"]\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.DependenciesCompatibility",
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
public class SuiteSingleSchemaDependency : IClassFixture<SuiteSingleSchemaDependency.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSingleSchemaDependency(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoDependency()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"quux\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWrongType()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"quux\", \"bar\": 2}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWrongTypeOther()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 2, \"bar\": \"quux\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWrongTypeBoth()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"quux\", \"bar\": \"quux\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"bar\"]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foobar\"");
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
                "tests\\draft2019-09\\optional\\dependencies-compatibility.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"dependencies\": {\r\n                \"bar\": {\r\n                    \"properties\": {\r\n                        \"foo\": {\"type\": \"integer\"},\r\n                        \"bar\": {\"type\": \"integer\"}\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.DependenciesCompatibility",
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
public class SuiteBooleanSubschemas : IClassFixture<SuiteBooleanSubschemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteBooleanSubschemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestObjectWithPropertyHavingSchemaTrueIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestObjectWithPropertyHavingSchemaFalseIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": 2}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestObjectWithBothPropertiesIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmptyObjectIsValid()
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
                "tests\\draft2019-09\\optional\\dependencies-compatibility.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"dependencies\": {\r\n                \"foo\": true,\r\n                \"bar\": false\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.DependenciesCompatibility",
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
public class SuiteSchemaDependenciesWithEscapedCharacters : IClassFixture<SuiteSchemaDependenciesWithEscapedCharacters.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSchemaDependenciesWithEscapedCharacters(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestQuotedTab()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\tbar\": 1,\r\n                    \"a\": 2,\r\n                    \"b\": 3,\r\n                    \"c\": 4\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestQuotedQuote()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo'bar\": {\"foo\\\"bar\": 1}\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestQuotedTabInvalidUnderDependentSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\tbar\": 1,\r\n                    \"a\": 2\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestQuotedQuoteInvalidUnderDependentSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo'bar\": 1}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\dependencies-compatibility.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"dependencies\": {\r\n                \"foo\\tbar\": {\"minProperties\": 4},\r\n                \"foo'bar\": {\"required\": [\"foo\\\"bar\"]}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.DependenciesCompatibility",
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
