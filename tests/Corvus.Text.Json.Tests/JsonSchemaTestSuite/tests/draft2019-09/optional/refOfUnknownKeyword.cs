using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Optional.RefOfUnknownKeyword;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteReferenceOfARootArbitraryKeyword : IClassFixture<SuiteReferenceOfARootArbitraryKeyword.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteReferenceOfARootArbitraryKeyword(Fixture fixture)
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
                "tests\\draft2019-09\\optional\\refOfUnknownKeyword.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"unknown-keyword\": {\"type\": \"integer\"},\r\n            \"properties\": {\r\n                \"bar\": {\"$ref\": \"#/unknown-keyword\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.RefOfUnknownKeyword",
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
public class SuiteReferenceOfARootArbitraryKeywordWithEncodedRef : IClassFixture<SuiteReferenceOfARootArbitraryKeywordWithEncodedRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteReferenceOfARootArbitraryKeywordWithEncodedRef(Fixture fixture)
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
                "tests\\draft2019-09\\optional\\refOfUnknownKeyword.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"unknown/keyword\": {\"type\": \"integer\"},\r\n            \"properties\": {\r\n                \"bar\": {\"$ref\": \"#/unknown~1keyword\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.RefOfUnknownKeyword",
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
public class SuiteReferenceOfAnArbitraryKeywordOfASubSchema : IClassFixture<SuiteReferenceOfAnArbitraryKeywordOfASubSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteReferenceOfAnArbitraryKeywordOfASubSchema(Fixture fixture)
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
                "tests\\draft2019-09\\optional\\refOfUnknownKeyword.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\"unknown-keyword\": {\"type\": \"integer\"}},\r\n                \"bar\": {\"$ref\": \"#/properties/foo/unknown-keyword\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.RefOfUnknownKeyword",
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
public class SuiteReferenceInternalsOfKnownNonApplicator : IClassFixture<SuiteReferenceInternalsOfKnownNonApplicator.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteReferenceInternalsOfKnownNonApplicator(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a string\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMismatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("42");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\refOfUnknownKeyword.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"/base\",\r\n            \"examples\": [\r\n              { \"type\": \"string\" }\r\n            ],\r\n            \"$ref\": \"#/examples/0\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.RefOfUnknownKeyword",
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
public class SuiteReferenceOfAnArbitraryKeywordOfASubSchemaWithEncodedRef : IClassFixture<SuiteReferenceOfAnArbitraryKeywordOfASubSchemaWithEncodedRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteReferenceOfAnArbitraryKeywordOfASubSchemaWithEncodedRef(Fixture fixture)
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
                "tests\\draft2019-09\\optional\\refOfUnknownKeyword.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\"unknown/keyword\": {\"type\": \"integer\"}},\r\n                \"bar\": {\"$ref\": \"#/properties/foo/unknown~1keyword\"}\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.RefOfUnknownKeyword",
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
