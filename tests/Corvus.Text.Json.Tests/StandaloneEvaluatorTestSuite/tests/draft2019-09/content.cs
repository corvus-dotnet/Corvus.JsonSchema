using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Content;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidJsonDocumentValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{:}\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNonStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("100");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\content.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"contentMediaType\": \"application/json\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJmb28iOiAiYmFyIn0K\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidBase64StringIsNotAValidCharacterValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJmb28iOi%iYmFyIn0K\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNonStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("100");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\content.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"contentEncoding\": \"base64\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJmb28iOiAiYmFyIn0K\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidlyEncodedInvalidJsonDocumentValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ezp9Cg==\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidBase64StringThatIsValidJsonValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{}\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNonStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("100");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\content.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"contentMediaType\": \"application/json\",\r\n            \"contentEncoding\": \"base64\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJmb28iOiAiYmFyIn0K\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnotherValidBase64EncodedJsonDocument()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJib28iOiAyMCwgImZvbyI6ICJiYXoifQ==\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidBase64EncodedJsonDocumentValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJib28iOiAyMH0=\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnEmptyObjectAsABase64EncodedJsonDocumentValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"e30=\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnEmptyArrayAsABase64EncodedJsonDocument()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"W10=\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidlyEncodedInvalidJsonDocumentValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ezp9Cg==\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidBase64StringThatIsValidJsonValidatesTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{}\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNonStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("100");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\content.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"contentMediaType\": \"application/json\",\r\n            \"contentEncoding\": \"base64\",\r\n            \"contentSchema\": { \"type\": \"object\", \"required\": [\"foo\"], \"properties\": { \"foo\": { \"type\": \"string\" } } }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
