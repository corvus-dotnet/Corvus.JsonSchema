using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Content;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteValidationOfStringEncodedContentBasedOnMediaType : IClassFixture<SuiteValidationOfStringEncodedContentBasedOnMediaType.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfStringEncodedContentBasedOnMediaType(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAValidJsonDocument()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"{\\\"foo\\\": \\\"bar\\\"}\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidJsonDocumentValidatesTrue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"{:}\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonStrings()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("100");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\content.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contentMediaType\": \"application/json\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Content",
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
public class SuiteValidationOfBinaryStringEncoding : IClassFixture<SuiteValidationOfBinaryStringEncoding.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfBinaryStringEncoding(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAValidBase64String()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"eyJmb28iOiAiYmFyIn0K\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidBase64StringIsNotAValidCharacterValidatesTrue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"eyJmb28iOi%iYmFyIn0K\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonStrings()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("100");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\content.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contentEncoding\": \"base64\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Content",
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
public class SuiteValidationOfBinaryEncodedMediaTypeDocuments : IClassFixture<SuiteValidationOfBinaryEncodedMediaTypeDocuments.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfBinaryEncodedMediaTypeDocuments(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAValidBase64EncodedJsonDocument()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"eyJmb28iOiAiYmFyIn0K\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidlyEncodedInvalidJsonDocumentValidatesTrue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"ezp9Cg==\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidBase64StringThatIsValidJsonValidatesTrue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"{}\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonStrings()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("100");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\content.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contentMediaType\": \"application/json\",\r\n            \"contentEncoding\": \"base64\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Content",
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
public class SuiteValidationOfBinaryEncodedMediaTypeDocumentsWithSchema : IClassFixture<SuiteValidationOfBinaryEncodedMediaTypeDocumentsWithSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfBinaryEncodedMediaTypeDocumentsWithSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAValidBase64EncodedJsonDocument()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"eyJmb28iOiAiYmFyIn0K\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnotherValidBase64EncodedJsonDocument()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"eyJib28iOiAyMCwgImZvbyI6ICJiYXoifQ==\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidBase64EncodedJsonDocumentValidatesTrue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"eyJib28iOiAyMH0=\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnEmptyObjectAsABase64EncodedJsonDocumentValidatesTrue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"e30=\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnEmptyArrayAsABase64EncodedJsonDocument()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"W10=\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidlyEncodedInvalidJsonDocumentValidatesTrue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"ezp9Cg==\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidBase64StringThatIsValidJsonValidatesTrue()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"{}\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNonStrings()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("100");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\content.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"contentMediaType\": \"application/json\",\r\n            \"contentEncoding\": \"base64\",\r\n            \"contentSchema\": { \"type\": \"object\", \"required\": [\"foo\"], \"properties\": { \"foo\": { \"type\": \"string\" } } }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Content",
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
