using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft4.Definitions;

[TestCategory("Draft4")]
[TestClass]
public class SuiteValidateDefinitionAgainstMetaschema
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
    public void TestValidDefinitionSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"definitions\": {\n                        \"foo\": {\"type\": \"integer\"}\n                    }\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidDefinitionSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"definitions\": {\n                        \"foo\": {\"type\": 1}\n                    }\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/definitions.json",
                "{\"$ref\": \"http://json-schema.org/draft-04/schema#\"}",
                "StandaloneEvaluatorTestSuite.Draft4.Definitions",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
