using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft4.Default;

[TestCategory("Draft4")]
[TestClass]
public class SuiteInvalidTypeForDefault
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
    public void TestValidWhenPropertyIsSpecified()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 13}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStillValidWhenTheInvalidDefaultIsUsed()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/default.json",
                "{\n            \"properties\": {\n                \"foo\": {\n                    \"type\": \"integer\",\n                    \"default\": []\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Default",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteInvalidStringValueForDefault
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
    public void TestValidWhenPropertyIsSpecified()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": \"good\"}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStillValidWhenTheInvalidDefaultIsUsed()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/default.json",
                "{\n            \"properties\": {\n                \"bar\": {\n                    \"type\": \"string\",\n                    \"minLength\": 4,\n                    \"default\": \"bad\"\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Default",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteTheDefaultKeywordDoesNotDoAnythingIfThePropertyIsMissing
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
    public void TestAnExplicitPropertyValueIsCheckedAgainstMaximumPassing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"alpha\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnExplicitPropertyValueIsCheckedAgainstMaximumFailing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"alpha\": 5 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMissingPropertiesAreNotFilledInWithTheDefault()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/default.json",
                "{\n            \"type\": \"object\",\n            \"properties\": {\n                \"alpha\": {\n                    \"type\": \"number\",\n                    \"maximum\": 3,\n                    \"default\": 5\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Default",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
