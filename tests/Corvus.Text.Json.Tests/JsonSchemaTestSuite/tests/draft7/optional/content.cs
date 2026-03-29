using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.Content;

[Trait("JsonSchemaTestSuite", "Draft7")]
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
    public void TestAnInvalidJsonDocument()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"{:}\"");
        Assert.False(dynamicInstance.EvaluateSchema());
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
                "tests\\draft7\\optional\\content.json",
                "{\r\n            \"contentMediaType\": \"application/json\"\r\n        }",
                "JsonSchemaTestSuite.Draft7.Optional.Content",
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
    public void TestAnInvalidBase64StringIsNotAValidCharacter()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"eyJmb28iOi%iYmFyIn0K\"");
        Assert.False(dynamicInstance.EvaluateSchema());
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
                "tests\\draft7\\optional\\content.json",
                "{\r\n            \"contentEncoding\": \"base64\"\r\n        }",
                "JsonSchemaTestSuite.Draft7.Optional.Content",
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
    public void TestAValidlyEncodedInvalidJsonDocument()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"ezp9Cg==\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidBase64StringThatIsValidJson()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"{}\"");
        Assert.False(dynamicInstance.EvaluateSchema());
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
                "tests\\draft7\\optional\\content.json",
                "{\r\n            \"contentMediaType\": \"application/json\",\r\n            \"contentEncoding\": \"base64\"\r\n        }",
                "JsonSchemaTestSuite.Draft7.Optional.Content",
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
