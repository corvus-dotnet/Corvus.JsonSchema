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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft4\\default.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"type\": \"integer\",\r\n                    \"default\": []\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft4\\default.json",
                "{\r\n            \"properties\": {\r\n                \"bar\": {\r\n                    \"type\": \"string\",\r\n                    \"minLength\": 4,\r\n                    \"default\": \"bad\"\r\n                }\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft4\\default.json",
                "{\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"alpha\": {\r\n                    \"type\": \"number\",\r\n                    \"maximum\": 3,\r\n                    \"default\": 5\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Default",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
