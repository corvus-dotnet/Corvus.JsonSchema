using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.FloatOverflow;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAllIntegersAreMultiplesOf05IfOverflowIsHandled : IClassFixture<SuiteAllIntegersAreMultiplesOf05IfOverflowIsHandled.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllIntegersAreMultiplesOf05IfOverflowIsHandled(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidIfOptionalOverflowHandlingIsImplemented()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1e308");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\float-overflow.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"integer\", \"multipleOf\": 0.5\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.FloatOverflow",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
