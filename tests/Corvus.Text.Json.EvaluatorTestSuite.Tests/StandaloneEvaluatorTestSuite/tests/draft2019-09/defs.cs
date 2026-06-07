using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.Defs;

[TestCategory("Draft201909")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"$defs\": {\"foo\": {\"type\": \"integer\"}}}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidDefinitionSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"$defs\": {\"foo\": {\"type\": 1}}}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/defs.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$ref\": \"https://json-schema.org/draft/2019-09/schema\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Defs",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
