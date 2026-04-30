using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Vocabulary;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteSchemaThatUsesCustomMetaschemaWithWithNoValidationVocabulary : IClassFixture<SuiteSchemaThatUsesCustomMetaschemaWithWithNoValidationVocabulary.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSchemaThatUsesCustomMetaschemaWithWithNoValidationVocabulary(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestApplicatorVocabularyStillWorks()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"badProperty\": \"this property should not exist\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoValidationValidNumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"numberProperty\": 20\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoValidationInvalidNumberButItStillValidates()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"numberProperty\": 1\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\vocabulary.json",
                "{\r\n            \"$id\": \"https://schema/using/no/validation\",\r\n            \"$schema\": \"http://localhost:1234/draft2019-09/metaschema-no-validation.json\",\r\n            \"properties\": {\r\n                \"badProperty\": false,\r\n                \"numberProperty\": {\r\n                    \"minimum\": 10\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Vocabulary",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteIgnoreUnrecognizedOptionalVocabulary : IClassFixture<SuiteIgnoreUnrecognizedOptionalVocabulary.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIgnoreUnrecognizedOptionalVocabulary(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestStringValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foobar\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNumberValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("20");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\vocabulary.json",
                "{\r\n             \"$schema\": \"http://localhost:1234/draft2019-09/metaschema-optional-vocabulary.json\",\r\n             \"type\": \"number\"\r\n         }",
                "StandaloneEvaluatorTestSuite.Draft201909.Vocabulary",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
