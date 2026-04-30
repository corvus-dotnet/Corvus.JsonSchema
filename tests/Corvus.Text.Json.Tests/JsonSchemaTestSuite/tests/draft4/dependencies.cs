using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft4.Dependencies;

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuiteDependencies : IClassFixture<SuiteDependencies.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDependencies(Fixture fixture)
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
                "tests\\draft4\\dependencies.json",
                "{\r\n            \"dependencies\": {\"bar\": [\"foo\"]}\r\n        }",
                "JsonSchemaTestSuite.Draft4.Dependencies",
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
public class SuiteMultipleDependencies : IClassFixture<SuiteMultipleDependencies.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMultipleDependencies(Fixture fixture)
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
                "tests\\draft4\\dependencies.json",
                "{\r\n            \"dependencies\": {\"quux\": [\"foo\", \"bar\"]}\r\n        }",
                "JsonSchemaTestSuite.Draft4.Dependencies",
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
public class SuiteMultipleDependenciesSubschema : IClassFixture<SuiteMultipleDependenciesSubschema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteMultipleDependenciesSubschema(Fixture fixture)
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

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\dependencies.json",
                "{\r\n            \"dependencies\": {\r\n                \"bar\": {\r\n                    \"properties\": {\r\n                        \"foo\": {\"type\": \"integer\"},\r\n                        \"bar\": {\"type\": \"integer\"}\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.Dependencies",
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
public class SuiteDependenciesWithEscapedCharacters : IClassFixture<SuiteDependenciesWithEscapedCharacters.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDependenciesWithEscapedCharacters(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidObject1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\nbar\": 1,\r\n                    \"foo\\rbar\": 2\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidObject2()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\tbar\": 1,\r\n                    \"a\": 2,\r\n                    \"b\": 3,\r\n                    \"c\": 4\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidObject3()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo'bar\": 1,\r\n                    \"foo\\\"bar\": 2\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidObject1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\nbar\": 1,\r\n                    \"foo\": 2\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidObject2()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\tbar\": 1,\r\n                    \"a\": 2\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidObject3()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo'bar\": 1\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidObject4()
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
                "tests\\draft4\\dependencies.json",
                "{\r\n            \"dependencies\": {\r\n                \"foo\\nbar\": [\"foo\\rbar\"],\r\n                \"foo\\tbar\": {\r\n                    \"minProperties\": 4\r\n                },\r\n                \"foo'bar\": {\"required\": [\"foo\\\"bar\"]},\r\n                \"foo\\\"bar\": [\"foo'bar\"]\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.Dependencies",
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
public class SuiteDependentSubschemaIncompatibleWithRoot : IClassFixture<SuiteDependentSubschemaIncompatibleWithRoot.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDependentSubschemaIncompatibleWithRoot(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesRoot()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchesDependency()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": 1}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchesBoth()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": 2}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoDependency()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"baz\": 1}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\dependencies.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {}\r\n            },\r\n            \"dependencies\": {\r\n                \"foo\": {\r\n                    \"properties\": {\r\n                        \"bar\": {}\r\n                    },\r\n                    \"additionalProperties\": false\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.Dependencies",
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
