using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Ref;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteRootPointerRef : IClassFixture<SuiteRootPointerRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRootPointerRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": false}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRecursiveMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": {\"foo\": false}}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMismatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": false}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRecursiveMismatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": {\"bar\": false}}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#\"}\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRelativePointerRefToObject : IClassFixture<SuiteRelativePointerRefToObject.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRelativePointerRefToObject(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": 3}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMismatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": true}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\"type\": \"integer\"},\r\n                \"bar\": {\"$ref\": \"#/properties/foo\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRelativePointerRefToArray : IClassFixture<SuiteRelativePointerRefToArray.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRelativePointerRefToArray(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchArray()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, 2]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMismatchArray()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[1, \"foo\"]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [\r\n                {\"type\": \"integer\"},\r\n                {\"$ref\": \"#/items/0\"}\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteEscapedPointerRef : IClassFixture<SuiteEscapedPointerRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEscapedPointerRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestSlashInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"slash\": \"aoeu\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTildeInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"tilde\": \"aoeu\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPercentInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"percent\": \"aoeu\"}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSlashValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"slash\": 123}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTildeValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"tilde\": 123}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPercentValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"percent\": 123}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$defs\": {\r\n                \"tilde~field\": {\"type\": \"integer\"},\r\n                \"slash/field\": {\"type\": \"integer\"},\r\n                \"percent%field\": {\"type\": \"integer\"}\r\n            },\r\n            \"properties\": {\r\n                \"tilde\": {\"$ref\": \"#/$defs/tilde~0field\"},\r\n                \"slash\": {\"$ref\": \"#/$defs/slash~1field\"},\r\n                \"percent\": {\"$ref\": \"#/$defs/percent%25field\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteNestedRefs : IClassFixture<SuiteNestedRefs.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNestedRefs(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNestedRefValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("5");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNestedRefInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$defs\": {\r\n                \"a\": {\"type\": \"integer\"},\r\n                \"b\": {\"$ref\": \"#/$defs/a\"},\r\n                \"c\": {\"$ref\": \"#/$defs/b\"}\r\n            },\r\n            \"$ref\": \"#/$defs/c\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRefAppliesAlongsideSiblingKeywords : IClassFixture<SuiteRefAppliesAlongsideSiblingKeywords.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefAppliesAlongsideSiblingKeywords(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRefValidMaxItemsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": [] }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRefValidMaxItemsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": [1, 2, 3] }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRefInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": \"string\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$defs\": {\r\n                \"reffed\": {\r\n                    \"type\": \"array\"\r\n                }\r\n            },\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"$ref\": \"#/$defs/reffed\",\r\n                    \"maxItems\": 2\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRemoteRefContainingRefsItself : IClassFixture<SuiteRemoteRefContainingRefsItself.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRemoteRefContainingRefsItself(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRemoteRefValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"minLength\": 1}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRemoteRefInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"minLength\": -1}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$ref\": \"https://json-schema.org/draft/2019-09/schema\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuitePropertyNamedRefThatIsNotAReference : IClassFixture<SuitePropertyNamedRefThatIsNotAReference.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertyNamedRefThatIsNotAReference(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPropertyNamedRefValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"$ref\": \"a\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPropertyNamedRefInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"$ref\": 2}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"$ref\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuitePropertyNamedRefContainingAnActualRef : IClassFixture<SuitePropertyNamedRefContainingAnActualRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertyNamedRefContainingAnActualRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPropertyNamedRefValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"$ref\": \"a\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestPropertyNamedRefInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"$ref\": 2}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"$ref\": {\"$ref\": \"#/$defs/is-string\"}\r\n            },\r\n            \"$defs\": {\r\n                \"is-string\": {\r\n                    \"type\": \"string\"\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRefToBooleanSchemaTrue : IClassFixture<SuiteRefToBooleanSchemaTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefToBooleanSchemaTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyValueIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$ref\": \"#/$defs/bool\",\r\n            \"$defs\": {\r\n                \"bool\": true\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRefToBooleanSchemaFalse : IClassFixture<SuiteRefToBooleanSchemaFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefToBooleanSchemaFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyValueIsInvalid()
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
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$ref\": \"#/$defs/bool\",\r\n            \"$defs\": {\r\n                \"bool\": false\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRecursiveReferencesBetweenSchemas : IClassFixture<SuiteRecursiveReferencesBetweenSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRecursiveReferencesBetweenSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidTree()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"meta\": \"root\",\r\n                    \"nodes\": [\r\n                        {\r\n                            \"value\": 1,\r\n                            \"subtree\": {\r\n                                \"meta\": \"child\",\r\n                                \"nodes\": [\r\n                                    {\"value\": 1.1},\r\n                                    {\"value\": 1.2}\r\n                                ]\r\n                            }\r\n                        },\r\n                        {\r\n                            \"value\": 2,\r\n                            \"subtree\": {\r\n                                \"meta\": \"child\",\r\n                                \"nodes\": [\r\n                                    {\"value\": 2.1},\r\n                                    {\"value\": 2.2}\r\n                                ]\r\n                            }\r\n                        }\r\n                    ]\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidTree()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"meta\": \"root\",\r\n                    \"nodes\": [\r\n                        {\r\n                            \"value\": 1,\r\n                            \"subtree\": {\r\n                                \"meta\": \"child\",\r\n                                \"nodes\": [\r\n                                    {\"value\": \"string is invalid\"},\r\n                                    {\"value\": 1.2}\r\n                                ]\r\n                            }\r\n                        },\r\n                        {\r\n                            \"value\": 2,\r\n                            \"subtree\": {\r\n                                \"meta\": \"child\",\r\n                                \"nodes\": [\r\n                                    {\"value\": 2.1},\r\n                                    {\"value\": 2.2}\r\n                                ]\r\n                            }\r\n                        }\r\n                    ]\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2019-09/tree\",\r\n            \"description\": \"tree of nodes\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"meta\": {\"type\": \"string\"},\r\n                \"nodes\": {\r\n                    \"type\": \"array\",\r\n                    \"items\": {\"$ref\": \"node\"}\r\n                }\r\n            },\r\n            \"required\": [\"meta\", \"nodes\"],\r\n            \"$defs\": {\r\n                \"node\": {\r\n                    \"$id\": \"http://localhost:1234/draft2019-09/node\",\r\n                    \"description\": \"node\",\r\n                    \"type\": \"object\",\r\n                    \"properties\": {\r\n                        \"value\": {\"type\": \"number\"},\r\n                        \"subtree\": {\"$ref\": \"tree\"}\r\n                    },\r\n                    \"required\": [\"value\"]\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRefsWithQuote : IClassFixture<SuiteRefsWithQuote.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefsWithQuote(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestObjectWithNumbersIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\\"bar\": 1\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestObjectWithStringsIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\\\"bar\": \"1\"\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"foo\\\"bar\": {\"$ref\": \"#/$defs/foo%22bar\"}\r\n            },\r\n            \"$defs\": {\r\n                \"foo\\\"bar\": {\"type\": \"number\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRefCreatesNewScopeWhenAdjacentToKeywords : IClassFixture<SuiteRefCreatesNewScopeWhenAdjacentToKeywords.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefCreatesNewScopeWhenAdjacentToKeywords(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestReferencedSubschemaDoesnTSeeAnnotationsFromProperties()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"prop1\": \"match\"\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$defs\": {\r\n                \"A\": {\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            },\r\n            \"properties\": {\r\n                \"prop1\": {\r\n                    \"type\": \"string\"\r\n                }\r\n            },\r\n            \"$ref\": \"#/$defs/A\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteNaiveReplacementOfRefWithItsDestinationIsNotCorrect : IClassFixture<SuiteNaiveReplacementOfRefWithItsDestinationIsNotCorrect.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNaiveReplacementOfRefWithItsDestinationIsNotCorrect(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestDoNotEvaluateTheRefInsideTheEnumMatchingAnyString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"this is a string\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDoNotEvaluateTheRefInsideTheEnumDefinitionExactMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"type\": \"string\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchTheEnumExactly()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"$ref\": \"#/$defs/a_string\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$defs\": {\r\n                \"a_string\": { \"type\": \"string\" }\r\n            },\r\n            \"enum\": [\r\n                { \"$ref\": \"#/$defs/a_string\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRefsWithRelativeUrisAndDefs : IClassFixture<SuiteRefsWithRelativeUrisAndDefs.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefsWithRelativeUrisAndDefs(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestInvalidOnInnerField()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": {\r\n                        \"bar\": 1\r\n                    },\r\n                    \"bar\": \"a\"\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidOnOuterField()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": {\r\n                        \"bar\": \"a\"\r\n                    },\r\n                    \"bar\": 1\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidOnBothFields()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": {\r\n                        \"bar\": \"a\"\r\n                    },\r\n                    \"bar\": \"a\"\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://example.com/schema-relative-uri-defs1.json\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"$id\": \"schema-relative-uri-defs2.json\",\r\n                    \"$defs\": {\r\n                        \"inner\": {\r\n                            \"properties\": {\r\n                                \"bar\": { \"type\": \"string\" }\r\n                            }\r\n                        }\r\n                    },\r\n                    \"$ref\": \"#/$defs/inner\"\r\n                }\r\n            },\r\n            \"$ref\": \"schema-relative-uri-defs2.json\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRelativeRefsWithAbsoluteUrisAndDefs : IClassFixture<SuiteRelativeRefsWithAbsoluteUrisAndDefs.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRelativeRefsWithAbsoluteUrisAndDefs(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestInvalidOnInnerField()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": {\r\n                        \"bar\": 1\r\n                    },\r\n                    \"bar\": \"a\"\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidOnOuterField()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": {\r\n                        \"bar\": \"a\"\r\n                    },\r\n                    \"bar\": 1\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidOnBothFields()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": {\r\n                        \"bar\": \"a\"\r\n                    },\r\n                    \"bar\": \"a\"\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://example.com/schema-refs-absolute-uris-defs1.json\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"$id\": \"http://example.com/schema-refs-absolute-uris-defs2.json\",\r\n                    \"$defs\": {\r\n                        \"inner\": {\r\n                            \"properties\": {\r\n                                \"bar\": { \"type\": \"string\" }\r\n                            }\r\n                        }\r\n                    },\r\n                    \"$ref\": \"#/$defs/inner\"\r\n                }\r\n            },\r\n            \"$ref\": \"schema-refs-absolute-uris-defs2.json\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteIdMustBeResolvedAgainstNearestParentNotJustImmediateParent : IClassFixture<SuiteIdMustBeResolvedAgainstNearestParentNotJustImmediateParent.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIdMustBeResolvedAgainstNearestParentNotJustImmediateParent(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonNumberIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://example.com/a.json\",\r\n            \"$defs\": {\r\n                \"x\": {\r\n                    \"$id\": \"http://example.com/b/c.json\",\r\n                    \"not\": {\r\n                        \"$defs\": {\r\n                            \"y\": {\r\n                                \"$id\": \"d.json\",\r\n                                \"type\": \"number\"\r\n                            }\r\n                        }\r\n                    }\r\n                }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"$ref\": \"http://example.com/b/d.json\"\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteOrderOfEvaluationIdAndRef : IClassFixture<SuiteOrderOfEvaluationIdAndRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteOrderOfEvaluationIdAndRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestDataIsValidAgainstFirstDefinition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("5");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDataIsInvalidAgainstFirstDefinition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("50");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$comment\": \"$id must be evaluated before $ref to get the proper $ref destination\",\r\n            \"$id\": \"https://example.com/draft2019-09/ref-and-id1/base.json\",\r\n            \"$ref\": \"int.json\",\r\n            \"$defs\": {\r\n                \"bigint\": {\r\n                    \"$comment\": \"canonical uri: https://example.com/draft2019-09/ref-and-id1/int.json\",\r\n                    \"$id\": \"int.json\",\r\n                    \"maximum\": 10\r\n                },\r\n                \"smallint\": {\r\n                    \"$comment\": \"canonical uri: https://example.com/draft2019-09/ref-and-id1-int.json\",\r\n                    \"$id\": \"/draft2019-09/ref-and-id1-int.json\",\r\n                    \"maximum\": 2\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteOrderOfEvaluationIdAndAnchorAndRef : IClassFixture<SuiteOrderOfEvaluationIdAndAnchorAndRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteOrderOfEvaluationIdAndAnchorAndRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestDataIsValidAgainstFirstDefinition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("5");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDataIsInvalidAgainstFirstDefinition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("50");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$comment\": \"$id must be evaluated before $ref to get the proper $ref destination\",\r\n            \"$id\": \"https://example.com/draft2019-09/ref-and-id2/base.json\",\r\n            \"$ref\": \"#bigint\",\r\n            \"$defs\": {\r\n                \"bigint\": {\r\n                    \"$comment\": \"canonical uri: https://example.com/draft2019-09/ref-and-id2/base.json#/$defs/bigint; another valid uri for this location: https://example.com/ref-and-id2/base.json#bigint\",\r\n                    \"$anchor\": \"bigint\",\r\n                    \"maximum\": 10\r\n                },\r\n                \"smallint\": {\r\n                    \"$comment\": \"canonical uri: https://example.com/draft2019-09/ref-and-id2#/$defs/smallint; another valid uri for this location: https://example.com/ref-and-id2/#bigint\",\r\n                    \"$id\": \"/draft2019-09/ref-and-id2/\",\r\n                    \"$anchor\": \"bigint\",\r\n                    \"maximum\": 2\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteOrderOfEvaluationIdAndRefOnNestedSchema : IClassFixture<SuiteOrderOfEvaluationIdAndRefOnNestedSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteOrderOfEvaluationIdAndRefOnNestedSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestDataIsValidAgainstNestedSibling()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("5");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDataIsInvalidAgainstNestedSibling()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$comment\": \"$id must be evaluated before $ref to get the proper $ref destination\",\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"https://example.com/draft2019-09/ref-and-id3/base.json\",\r\n            \"$ref\": \"nested/foo.json\",\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$comment\": \"canonical uri: https://example.com/draft2019-09/ref-and-id3/nested/foo.json\",\r\n                    \"$id\": \"nested/foo.json\",\r\n                    \"$ref\": \"./bar.json\"\r\n                },\r\n                \"bar\": {\r\n                    \"$comment\": \"canonical uri: https://example.com/draft2019-09/ref-and-id3/nested/bar.json\",\r\n                    \"$id\": \"nested/bar.json\",\r\n                    \"type\": \"number\"\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteSimpleUrnBaseUriWithRefViaTheUrn : IClassFixture<SuiteSimpleUrnBaseUriWithRefViaTheUrn.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSimpleUrnBaseUriWithRefViaTheUrn(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidUnderTheUrnIDedSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 37}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidUnderTheUrnIDedSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\r\n            \"$id\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\",\r\n            \"minimum\": 30,\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteSimpleUrnBaseUriWithJsonPointer : IClassFixture<SuiteSimpleUrnBaseUriWithJsonPointer.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSimpleUrnBaseUriWithJsonPointer(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"bar\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\r\n            \"$id\": \"urn:uuid:deadbeef-1234-00ff-ff00-4321feebdaed\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#/$defs/bar\"}\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteUrnBaseUriWithNss : IClassFixture<SuiteUrnBaseUriWithNss.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUrnBaseUriWithNss(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"bar\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$comment\": \"RFC 8141 §2.2\",\r\n            \"$id\": \"urn:example:1/406/47452/2\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#/$defs/bar\"}\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteUrnBaseUriWithRComponent : IClassFixture<SuiteUrnBaseUriWithRComponent.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUrnBaseUriWithRComponent(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"bar\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$comment\": \"RFC 8141 §2.3.1\",\r\n            \"$id\": \"urn:example:foo-bar-baz-qux?+CCResolve:cc=uk\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#/$defs/bar\"}\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteUrnBaseUriWithQComponent : IClassFixture<SuiteUrnBaseUriWithQComponent.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUrnBaseUriWithQComponent(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"bar\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$comment\": \"RFC 8141 §2.3.2\",\r\n            \"$id\": \"urn:example:weather?=op=map&lat=39.56&lon=-104.85&datetime=1969-07-21T02:56:15Z\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#/$defs/bar\"}\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteUrnBaseUriWithUrnAndJsonPointerRef : IClassFixture<SuiteUrnBaseUriWithUrnAndJsonPointerRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUrnBaseUriWithUrnAndJsonPointerRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"bar\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"urn:uuid:deadbeef-1234-0000-0000-4321feebdaed\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"urn:uuid:deadbeef-1234-0000-0000-4321feebdaed#/$defs/bar\"}\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteUrnBaseUriWithUrnAndAnchorRef : IClassFixture<SuiteUrnBaseUriWithUrnAndAnchorRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUrnBaseUriWithUrnAndAnchorRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": \"bar\"}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"foo\": 12}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed#something\"}\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\r\n                    \"$anchor\": \"something\",\r\n                    \"type\": \"string\"\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteUrnRefWithNestedPointerRef : IClassFixture<SuiteUrnRefWithNestedPointerRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUrnRefWithNestedPointerRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"bar\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$ref\": \"urn:uuid:deadbeef-4321-ffff-ffff-1234feebdaed\",\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$id\": \"urn:uuid:deadbeef-4321-ffff-ffff-1234feebdaed\",\r\n                    \"$defs\": {\"bar\": {\"type\": \"string\"}},\r\n                    \"$ref\": \"#/$defs/bar\"\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRefToIf : IClassFixture<SuiteRefToIf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefToIf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestANonIntegerIsInvalidDueToTheRef()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIntegerIsValid()
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
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$ref\": \"http://example.com/ref/if\",\r\n            \"if\": {\r\n                \"$id\": \"http://example.com/ref/if\",\r\n                \"type\": \"integer\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRefToThen : IClassFixture<SuiteRefToThen.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefToThen(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestANonIntegerIsInvalidDueToTheRef()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIntegerIsValid()
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
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$ref\": \"http://example.com/ref/then\",\r\n            \"then\": {\r\n                \"$id\": \"http://example.com/ref/then\",\r\n                \"type\": \"integer\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRefToElse : IClassFixture<SuiteRefToElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefToElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestANonIntegerIsInvalidDueToTheRef()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIntegerIsValid()
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
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$ref\": \"http://example.com/ref/else\",\r\n            \"else\": {\r\n                \"$id\": \"http://example.com/ref/else\",\r\n                \"type\": \"integer\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRefWithAbsolutePathReference : IClassFixture<SuiteRefWithAbsolutePathReference.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefWithAbsolutePathReference(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnIntegerIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n             \"$id\": \"http://example.com/ref/absref.json\",\r\n             \"$defs\": {\r\n                 \"a\": {\r\n                     \"$id\": \"http://example.com/ref/absref/foobar.json\",\r\n                     \"type\": \"number\"\r\n                 },\r\n                 \"b\": {\r\n                     \"$id\": \"http://example.com/absref/foobar.json\",\r\n                     \"type\": \"string\"\r\n                 }\r\n             },\r\n             \"$ref\": \"/absref/foobar.json\"\r\n         }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteIdWithFileUriStillResolvesPointersNix : IClassFixture<SuiteIdWithFileUriStillResolvesPointersNix.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIdWithFileUriStillResolvesPointersNix(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonNumberIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n             \"$id\": \"file:///folder/file.json\",\r\n             \"$defs\": {\r\n                 \"foo\": {\r\n                     \"type\": \"number\"\r\n                 }\r\n             },\r\n             \"$ref\": \"#/$defs/foo\"\r\n         }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteIdWithFileUriStillResolvesPointersWindows : IClassFixture<SuiteIdWithFileUriStillResolvesPointersWindows.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIdWithFileUriStillResolvesPointersWindows(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonNumberIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n             \"$id\": \"file:///c:/folder/file.json\",\r\n             \"$defs\": {\r\n                 \"foo\": {\r\n                     \"type\": \"number\"\r\n                 }\r\n             },\r\n             \"$ref\": \"#/$defs/foo\"\r\n         }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteEmptyTokensInRefJsonPointer : IClassFixture<SuiteEmptyTokensInRefJsonPointer.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEmptyTokensInRefJsonPointer(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonNumberIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n             \"$defs\": {\r\n                 \"\": {\r\n                     \"$defs\": {\r\n                         \"\": { \"type\": \"number\" }\r\n                     }\r\n                 } \r\n             },\r\n             \"allOf\": [\r\n                 {\r\n                     \"$ref\": \"#/$defs//$defs/\"\r\n                 }\r\n             ]\r\n         }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
public class SuiteRefWithRecursiveAnchor : IClassFixture<SuiteRefWithRecursiveAnchor.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefWithRecursiveAnchor(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestExtraItemsAllowedForInnerArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\",[\"bar\" , [] , 8]]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExtraItemsDisallowedForRoot()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[\"foo\",[\"bar\" , [] , 8], 8]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"https://example.com/schemas/unevaluated-items-are-disallowed\",\r\n            \"$ref\": \"/schemas/unevaluated-items-are-allowed\",\r\n            \"$recursiveAnchor\": true,\r\n            \"unevaluatedItems\": false,\r\n            \"$defs\": {\r\n                \"/schemas/unevaluated-items-are-allowed\": {\r\n                    \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n                    \"$id\": \"/schemas/unevaluated-items-are-allowed\",\r\n                    \"$recursiveAnchor\": true,\r\n                    \"type\": \"array\",\r\n                    \"items\": [\r\n                        {\r\n                            \"type\": \"string\"\r\n                        },\r\n                        {\r\n                            \"$ref\": \"#\"\r\n                        }\r\n                    ]\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Ref",
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
