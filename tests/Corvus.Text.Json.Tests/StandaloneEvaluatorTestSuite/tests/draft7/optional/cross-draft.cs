using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.CrossDraft;

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts : IClassFixture<SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMissingBarIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"any value\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPresentBarIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"any value\", \"bar\": \"also any value\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\optional\\cross-draft.json",
                "{\r\n            \"type\": \"object\",\r\n            \"allOf\": [\r\n                { \"properties\": { \"foo\": true } },\r\n                { \"$ref\": \"http://localhost:1234/draft2019-09/dependentRequired.json\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.CrossDraft",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
