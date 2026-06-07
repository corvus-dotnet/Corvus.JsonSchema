using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.NonBmpRegex;

[TestCategory("Draft7")]
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
                "tests/draft7/optional/non-bmp-regex.json",
                "{ \"pattern\": \"^🐲*$\" }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.NonBmpRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
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
                "tests/draft7/optional/non-bmp-regex.json",
                "{\n            \"patternProperties\": {\n                \"^🐲*$\": {\n                    \"type\": \"integer\"\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.NonBmpRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
