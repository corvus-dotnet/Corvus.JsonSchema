using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Anchor;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteLocationIndependentIdentifier : IClassFixture<SuiteLocationIndependentIdentifier.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteLocationIndependentIdentifier(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMismatch()
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
                "tests\\draft2020-12\\anchor.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"#foo\",\r\n            \"$defs\": {\r\n                \"A\": {\r\n                    \"$anchor\": \"foo\",\r\n                    \"type\": \"integer\"\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Anchor",
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
public class SuiteLocationIndependentIdentifierWithAbsoluteUri : IClassFixture<SuiteLocationIndependentIdentifierWithAbsoluteUri.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteLocationIndependentIdentifierWithAbsoluteUri(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMismatch()
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
                "tests\\draft2020-12\\anchor.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/bar#foo\",\r\n            \"$defs\": {\r\n                \"A\": {\r\n                    \"$id\": \"http://localhost:1234/draft2020-12/bar\",\r\n                    \"$anchor\": \"foo\",\r\n                    \"type\": \"integer\"\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Anchor",
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
public class SuiteLocationIndependentIdentifierWithBaseUriChangeInSubschema : IClassFixture<SuiteLocationIndependentIdentifierWithBaseUriChangeInSubschema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteLocationIndependentIdentifierWithBaseUriChangeInSubschema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMismatch()
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
                "tests\\draft2020-12\\anchor.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/root\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/nested.json#foo\",\r\n            \"$defs\": {\r\n                \"A\": {\r\n                    \"$id\": \"nested.json\",\r\n                    \"$defs\": {\r\n                        \"B\": {\r\n                            \"$anchor\": \"foo\",\r\n                            \"type\": \"integer\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Anchor",
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
public class SuiteSameAnchorWithDifferentBaseUri : IClassFixture<SuiteSameAnchorWithDifferentBaseUri.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSameAnchorWithDifferentBaseUri(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRefResolvesToDefsAAllOf1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestRefDoesNotResolveToDefsAAllOf0()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\anchor.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/foobar\",\r\n            \"$defs\": {\r\n                \"A\": {\r\n                    \"$id\": \"child1\",\r\n                    \"allOf\": [\r\n                        {\r\n                            \"$id\": \"child2\",\r\n                            \"$anchor\": \"my_anchor\",\r\n                            \"type\": \"number\"\r\n                        },\r\n                        {\r\n                            \"$anchor\": \"my_anchor\",\r\n                            \"type\": \"string\"\r\n                        }\r\n                    ]\r\n                }\r\n            },\r\n            \"$ref\": \"child1#my_anchor\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Anchor",
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
