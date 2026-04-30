using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.CrossDraft;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts : IClassFixture<SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestFirstItemNotAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\cross-draft.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"array\",\r\n            \"$ref\": \"http://localhost:1234/draft2019-09/ignore-prefixItems.json\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.CrossDraft",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
