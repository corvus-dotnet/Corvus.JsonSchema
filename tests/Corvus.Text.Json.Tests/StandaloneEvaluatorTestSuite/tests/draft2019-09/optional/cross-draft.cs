using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.CrossDraft;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts
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
                "tests\\draft2019-09\\optional\\cross-draft.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"array\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/prefixItems.json\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.CrossDraft",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts
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
    public void TestMissingBarIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"any value\"}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\cross-draft.json",
                "{\r\n            \"type\": \"object\",\r\n            \"allOf\": [\r\n                { \"properties\": { \"foo\": true } },\r\n                { \"$ref\": \"http://localhost:1234/draft7/ignore-dependentRequired.json\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.CrossDraft",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
