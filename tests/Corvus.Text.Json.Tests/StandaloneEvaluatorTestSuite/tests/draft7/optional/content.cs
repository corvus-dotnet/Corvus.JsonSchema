using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.Content;

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
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
    public void TestAnInvalidJsonDocument()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{:}\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
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
                "tests\\draft7\\optional\\content.json",
                "{\r\n            \"contentMediaType\": \"application/json\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
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
    public void TestAnInvalidBase64StringIsNotAValidCharacter()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"eyJmb28iOi%iYmFyIn0K\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
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
                "tests\\draft7\\optional\\content.json",
                "{\r\n            \"contentEncoding\": \"base64\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
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
    public void TestAValidlyEncodedInvalidJsonDocument()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ezp9Cg==\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidBase64StringThatIsValidJson()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{}\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
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
                "tests\\draft7\\optional\\content.json",
                "{\r\n            \"contentMediaType\": \"application/json\",\r\n            \"contentEncoding\": \"base64\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Content",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
