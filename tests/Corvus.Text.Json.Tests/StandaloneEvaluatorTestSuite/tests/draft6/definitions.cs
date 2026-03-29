using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft6.Definitions;

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteValidateDefinitionAgainstMetaschema : IClassFixture<SuiteValidateDefinitionAgainstMetaschema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidateDefinitionAgainstMetaschema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidDefinitionSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"definitions\": {\r\n                        \"foo\": {\"type\": \"integer\"}\r\n                    }\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidDefinitionSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"definitions\": {\r\n                        \"foo\": {\"type\": 1}\r\n                    }\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\definitions.json",
                "{\"$ref\": \"http://json-schema.org/draft-06/schema#\"}",
                "StandaloneEvaluatorTestSuite.Draft6.Definitions",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
