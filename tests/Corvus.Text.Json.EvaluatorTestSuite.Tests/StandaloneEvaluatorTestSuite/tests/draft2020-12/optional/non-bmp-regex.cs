using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.NonBmpRegex;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteProperUtf16SurrogatePairHandlingPattern
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
    public void TestMatchesEmpty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatchesSingle()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"🐲\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatchesTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"🐲🐲\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDoesnTMatchOne()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"🐉\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDoesnTMatchTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"🐉🐉\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDoesnTMatchOneAscii()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"D\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDoesnTMatchTwoAscii()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"DD\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/optional/non-bmp-regex.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"pattern\": \"^🐲*$\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.NonBmpRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteProperUtf16SurrogatePairHandlingPatternProperties
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
    public void TestMatchesEmpty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatchesSingle()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"🐲\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatchesTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"🐲🐲\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDoesnTMatchOne()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"🐲\": \"hello\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDoesnTMatchTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"🐲🐲\": \"hello\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/optional/non-bmp-regex.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"patternProperties\": {\n                \"^🐲*$\": {\n                    \"type\": \"integer\"\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.NonBmpRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
