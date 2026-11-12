using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.CrossDraft;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts7
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
    public void TestMissingBarIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"any value\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/optional/cross-draft.json",
                "{\n                \"type\": \"object\",\n                \"allOf\": [\n                    { \"properties\": { \"foo\": true } },\n                    { \"$ref\": \"http://localhost:1234/draft7/ignore-dependentRequired.json\" }\n                ]\n            }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.CrossDraft",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/draft7/ignore-dependentRequired.json\": {\n                    \"$schema\": \"http://json-schema.org/draft-07/schema#\",\n                    \"dependentRequired\": {\n                        \"foo\": [\"bar\"]\n                    }\n                }\n            }");
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts2019
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
    public void TestFirstItemNotAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/optional/cross-draft.json",
                "{\n                \"type\": \"array\",\n                \"$ref\": \"http://localhost:1234/draft2019-09/ignore-prefixItems.json\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.CrossDraft",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/draft2019-09/ignore-prefixItems.json\": {\n                    \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n                    \"prefixItems\": [\n                        { \"type\": \"string\" }\n                    ]\n                }\n            }");
        }
    }
}
