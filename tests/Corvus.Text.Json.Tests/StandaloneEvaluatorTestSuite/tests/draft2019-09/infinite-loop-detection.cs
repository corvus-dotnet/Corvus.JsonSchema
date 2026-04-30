using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.InfiniteLoopDetection;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteEvaluatingTheSameSchemaLocationAgainstTheSameDataLocationTwiceIsNotASignOfAnInfiniteLoop : IClassFixture<SuiteEvaluatingTheSameSchemaLocationAgainstTheSameDataLocationTwiceIsNotASignOfAnInfiniteLoop.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEvaluatingTheSameSchemaLocationAgainstTheSameDataLocationTwiceIsNotASignOfAnInfiniteLoop(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPassingCase()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFailingCase()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"a string\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\infinite-loop-detection.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$defs\": {\r\n                \"int\": { \"type\": \"integer\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": {\r\n                            \"$ref\": \"#/$defs/int\"\r\n                        }\r\n                    }\r\n                },\r\n                {\r\n                    \"additionalProperties\": {\r\n                        \"$ref\": \"#/$defs/int\"\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.InfiniteLoopDetection",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
