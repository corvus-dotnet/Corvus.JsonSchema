using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.InfiniteLoopDetection;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteEvaluatingTheSameSchemaLocationAgainstTheSameDataLocationTwiceIsNotASignOfAnInfiniteLoop
{
    private static Fixture? s_fixture;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        s_fixture = null;
    }

    [TestMethod]
    public void TestPassingCase()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFailingCase()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"a string\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/infinite-loop-detection.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$defs\": {\n                \"int\": { \"type\": \"integer\" }\n            },\n            \"allOf\": [\n                {\n                    \"properties\": {\n                        \"foo\": {\n                            \"$ref\": \"#/$defs/int\"\n                        }\n                    }\n                },\n                {\n                    \"additionalProperties\": {\n                        \"$ref\": \"#/$defs/int\"\n                    }\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.InfiniteLoopDetection",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
