using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.CrossDraft;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts : IClassFixture<SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFirstItemNotAStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFirstItemIsAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"a string\", 1, 2, 3]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\cross-draft.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"array\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/prefixItems.json\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.CrossDraft",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts : IClassFixture<SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMissingBarIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"any value\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\cross-draft.json",
                "{\r\n            \"type\": \"object\",\r\n            \"allOf\": [\r\n                { \"properties\": { \"foo\": true } },\r\n                { \"$ref\": \"http://localhost:1234/draft7/ignore-dependentRequired.json\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.CrossDraft",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
