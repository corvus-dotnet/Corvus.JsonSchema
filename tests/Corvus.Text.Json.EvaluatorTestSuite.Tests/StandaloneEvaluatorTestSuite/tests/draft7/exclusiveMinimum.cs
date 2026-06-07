using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.ExclusiveMinimum;

[TestCategory("Draft7")]
[TestClass]
public class SuiteExclusiveMinimumValidation
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
    public void TestAboveTheExclusiveMinimumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.2");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBoundaryPointIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.1");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBelowTheExclusiveMinimumIsInvalid()
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
                "tests/draft7/exclusiveMinimum.json",
                "{\n            \"exclusiveMinimum\": 1.1\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.ExclusiveMinimum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
