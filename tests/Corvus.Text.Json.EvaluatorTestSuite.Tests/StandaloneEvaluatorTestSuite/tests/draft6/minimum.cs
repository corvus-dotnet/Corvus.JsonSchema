using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft6.Minimum;

[TestCategory("Draft6")]
[TestClass]
public class SuiteMinimumValidation
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
    public void TestAboveTheMinimumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2.6");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBoundaryPointIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBelowTheMinimumIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0.6");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresNonNumbers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"x\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/minimum.json",
                "{\"minimum\": 1.1}",
                "StandaloneEvaluatorTestSuite.Draft6.Minimum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteMinimumValidationWithSignedInteger
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
    public void TestNegativeAboveTheMinimumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPositiveAboveTheMinimumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBoundaryPointIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-2");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBoundaryPointWithFloatIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-2.0");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFloatBelowTheMinimumIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-2.0001");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIntBelowTheMinimumIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-3");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresNonNumbers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"x\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/minimum.json",
                "{\"minimum\": -2}",
                "StandaloneEvaluatorTestSuite.Draft6.Minimum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
