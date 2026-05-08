using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.InfiniteLoopDetection;

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2020-12\\infinite-loop-detection.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"int\": { \"type\": \"integer\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": {\r\n                            \"$ref\": \"#/$defs/int\"\r\n                        }\r\n                    }\r\n                },\r\n                {\r\n                    \"additionalProperties\": {\r\n                        \"$ref\": \"#/$defs/int\"\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.InfiniteLoopDetection",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
