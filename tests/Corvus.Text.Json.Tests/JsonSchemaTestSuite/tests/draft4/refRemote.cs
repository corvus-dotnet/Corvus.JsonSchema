using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft4.RefRemote;

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuiteRemoteRef : IClassFixture<SuiteRemoteRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRemoteRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRemoteRefValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRemoteRefInvalid()
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
                "tests\\draft4\\refRemote.json",
                "{\"$ref\": \"http://localhost:1234/integer.json\"}",
                "JsonSchemaTestSuite.Draft4.RefRemote",
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
public class SuiteFragmentWithinRemoteRef : IClassFixture<SuiteFragmentWithinRemoteRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteFragmentWithinRemoteRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRemoteFragmentValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRemoteFragmentInvalid()
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
                "tests\\draft4\\refRemote.json",
                "{\"$ref\": \"http://localhost:1234/draft4/subSchemas.json#/definitions/integer\"}",
                "JsonSchemaTestSuite.Draft4.RefRemote",
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
public class SuiteRefWithinRemoteRef : IClassFixture<SuiteRefWithinRemoteRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefWithinRemoteRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRefWithinRefValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRefWithinRefInvalid()
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
                "tests\\draft4\\refRemote.json",
                "{\r\n            \"$ref\": \"http://localhost:1234/draft4/subSchemas.json#/definitions/refToInteger\"\r\n        }",
                "JsonSchemaTestSuite.Draft4.RefRemote",
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
public class SuiteBaseUriChange : IClassFixture<SuiteBaseUriChange.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteBaseUriChange(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBaseUriChangeRefValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[1]]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBaseUriChangeRefInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[[\"a\"]]");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\refRemote.json",
                "{\r\n            \"id\": \"http://localhost:1234/\",\r\n            \"items\": {\r\n                \"id\": \"baseUriChange/\",\r\n                \"items\": {\"$ref\": \"folderInteger.json\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.RefRemote",
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
public class SuiteBaseUriChangeChangeFolder : IClassFixture<SuiteBaseUriChangeChangeFolder.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteBaseUriChangeChangeFolder(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"list\": [1]}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"list\": [\"a\"]}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\refRemote.json",
                "{\r\n            \"id\": \"http://localhost:1234/scope_change_defs1.json\",\r\n            \"type\" : \"object\",\r\n            \"properties\": {\r\n                \"list\": {\"$ref\": \"#/definitions/baz\"}\r\n            },\r\n            \"definitions\": {\r\n                \"baz\": {\r\n                    \"id\": \"baseUriChangeFolder/\",\r\n                    \"type\": \"array\",\r\n                    \"items\": {\"$ref\": \"folderInteger.json\"}\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.RefRemote",
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
public class SuiteBaseUriChangeChangeFolderInSubschema : IClassFixture<SuiteBaseUriChangeChangeFolderInSubschema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteBaseUriChangeChangeFolderInSubschema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"list\": [1]}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestStringIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"list\": [\"a\"]}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\refRemote.json",
                "{\r\n            \"id\": \"http://localhost:1234/scope_change_defs2.json\",\r\n            \"type\" : \"object\",\r\n            \"properties\": {\r\n                \"list\": {\"$ref\": \"#/definitions/baz/definitions/bar\"}\r\n            },\r\n            \"definitions\": {\r\n                \"baz\": {\r\n                    \"id\": \"baseUriChangeFolderInSubschema/\",\r\n                    \"definitions\": {\r\n                        \"bar\": {\r\n                            \"type\": \"array\",\r\n                            \"items\": {\"$ref\": \"folderInteger.json\"}\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.RefRemote",
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
public class SuiteRootRefInRemoteRef : IClassFixture<SuiteRootRefInRemoteRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRootRefInRemoteRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"name\": \"foo\"\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNullIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"name\": null\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestObjectIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"name\": {\r\n                        \"name\": null\r\n                    }\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\refRemote.json",
                "{\r\n            \"id\": \"http://localhost:1234/object\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"name\": {\"$ref\": \"draft4/name.json#/definitions/orNull\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft4.RefRemote",
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
public class SuiteLocationIndependentIdentifierInRemoteRef : IClassFixture<SuiteLocationIndependentIdentifierInRemoteRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteLocationIndependentIdentifierInRemoteRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIntegerIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestStringIsInvalid()
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
                "tests\\draft4\\refRemote.json",
                "{\r\n            \"$ref\": \"http://localhost:1234/draft4/locationIndependentIdentifier.json#/definitions/refToInteger\"\r\n        }",
                "JsonSchemaTestSuite.Draft4.RefRemote",
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
