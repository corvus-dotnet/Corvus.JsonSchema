using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.CrossDraft;

[TestCategory("Draft7")]
[TestClass]
public class SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts2019
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
    public void TestMissingBarIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"any value\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPresentBarIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"any value\", \"bar\": \"also any value\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/optional/cross-draft.json",
                "{\n                \"type\": \"object\",\n                \"allOf\": [\n                    { \"properties\": { \"foo\": true } },\n                    { \"$ref\": \"http://localhost:1234/draft2019-09/dependentRequired.json\" }\n                ]\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.CrossDraft",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/draft2019-09/dependentRequired.json\": {\n                    \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n                    \"dependentRequired\": {\n                        \"foo\": [\"bar\"]\n                    }\n                }\n            }");
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts2020
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
    public void TestFirstItemNotAStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFirstItemIsAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"a string\", 1, 2, 3]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/optional/cross-draft.json",
                "{\n                \"type\": \"array\",\n                \"$ref\": \"http://localhost:1234/draft2020-12/prefixItems.json\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.CrossDraft",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/draft2020-12/prefixItems.json\": {\n                    \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n                    \"prefixItems\": [\n                        { \"type\": \"string\" }\n                    ]\n                }\n            }");
        }
    }
}
