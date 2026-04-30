using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.RefRemote;

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/integer.json\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/subSchemas.json#/$defs/integer\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
public class SuiteAnchorWithinRemoteRef : IClassFixture<SuiteAnchorWithinRemoteRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnchorWithinRemoteRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRemoteAnchorValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRemoteAnchorInvalid()
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/locationIndependentIdentifier.json#foo\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/subSchemas.json#/$defs/refToInteger\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/\",\r\n            \"items\": {\r\n                \"$id\": \"baseUriChange/\",\r\n                \"items\": {\"$ref\": \"folderInteger.json\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/scope_change_defs1.json\",\r\n            \"type\" : \"object\",\r\n            \"properties\": {\"list\": {\"$ref\": \"baseUriChangeFolder/\"}},\r\n            \"$defs\": {\r\n                \"baz\": {\r\n                    \"$id\": \"baseUriChangeFolder/\",\r\n                    \"type\": \"array\",\r\n                    \"items\": {\"$ref\": \"folderInteger.json\"}\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/scope_change_defs2.json\",\r\n            \"type\" : \"object\",\r\n            \"properties\": {\"list\": {\"$ref\": \"baseUriChangeFolderInSubschema/#/$defs/bar\"}},\r\n            \"$defs\": {\r\n                \"baz\": {\r\n                    \"$id\": \"baseUriChangeFolderInSubschema/\",\r\n                    \"$defs\": {\r\n                        \"bar\": {\r\n                            \"type\": \"array\",\r\n                            \"items\": {\"$ref\": \"folderInteger.json\"}\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/object\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"name\": {\"$ref\": \"name-defs.json#/$defs/orNull\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
public class SuiteRemoteRefWithRefToDefs : IClassFixture<SuiteRemoteRefWithRefToDefs.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRemoteRefWithRefToDefs(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"bar\": 1\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"bar\": \"a\"\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/schema-remote-ref-ref-defs1.json\",\r\n            \"$ref\": \"ref-and-defs.json\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/locationIndependentIdentifier.json#/$defs/refToInteger\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
public class SuiteRetrievedNestedRefsResolveRelativeToTheirUriNotId : IClassFixture<SuiteRetrievedNestedRefsResolveRelativeToTheirUriNotId.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRetrievedNestedRefsResolveRelativeToTheirUriNotId(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"name\": {\"foo\":  1}\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestStringIsValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"name\": {\"foo\":  \"a\"}\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/some-id\",\r\n            \"properties\": {\r\n                \"name\": {\"$ref\": \"nested/foo-ref-string.json\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
public class SuiteRemoteHttpRefWithDifferentId : IClassFixture<SuiteRemoteHttpRefWithDifferentId.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRemoteHttpRefWithDifferentId(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestStringIsValid()
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/different-id-ref-string.json\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
public class SuiteRemoteHttpRefWithDifferentUrnId : IClassFixture<SuiteRemoteHttpRefWithDifferentUrnId.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRemoteHttpRefWithDifferentUrnId(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestStringIsValid()
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/urn-ref-string.json\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
public class SuiteRemoteHttpRefWithNestedAbsoluteRef : IClassFixture<SuiteRemoteHttpRefWithNestedAbsoluteRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRemoteHttpRefWithNestedAbsoluteRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsInvalid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestStringIsValid()
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/nested-absolute-ref-to-string.json\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
public class SuiteRefToRefFindsDetachedAnchor : IClassFixture<SuiteRefToRefFindsDetachedAnchor.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefToRefFindsDetachedAnchor(Fixture fixture)
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
                "tests\\draft2020-12\\refRemote.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/detached-ref.json#/$defs/foo\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.RefRemote",
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
