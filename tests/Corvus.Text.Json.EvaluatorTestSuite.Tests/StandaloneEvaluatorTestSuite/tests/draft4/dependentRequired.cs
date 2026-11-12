using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft4.DependentRequired;

[TestCategory("Draft4")]
[TestClass]
public class SuiteSingleDependencyDependencies
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
    public void TestNeither()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNondependant()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithDependency()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1, \"bar\": 2 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMissingDependency()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"bar\"]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foobar\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresOtherNonObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/dependentRequired.json",
                "{\n                \"dependencies\": {\n                    \"bar\": [\"foo\"]\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.DependentRequired",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteMultipleDependentsRequiredDependencies
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
    public void TestNeither()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNondependants()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1, \"bar\": 2 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithDependencies()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1, \"bar\": 2, \"quux\": 3 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMissingDependency()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1, \"quux\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMissingOtherDependency()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": 1, \"quux\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMissingBothDependencies()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"quux\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/dependentRequired.json",
                "{\n                \"dependencies\": { \"quux\": [\"foo\", \"bar\"] }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.DependentRequired",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteDependenciesWithEscapedCharactersDependencies
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
    public void TestCrlf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\\nbar\": 1, \"foo\\rbar\": 2 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestQuotedQuotes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo'bar\": 1, \"foo\\\"bar\": 2 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCrlfMissingDependent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\\nbar\": 1, \"foo\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestQuotedQuotesMissingDependent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\\\"bar\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/dependentRequired.json",
                "{\n                \"dependencies\": {\n                    \"foo\\nbar\": [\"foo\\rbar\"],\n                    \"foo\\\"bar\": [\"foo'bar\"]\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.DependentRequired",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
