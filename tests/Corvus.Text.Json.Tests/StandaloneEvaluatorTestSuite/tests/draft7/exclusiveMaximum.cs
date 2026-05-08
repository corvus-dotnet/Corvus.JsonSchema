using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.ExclusiveMaximum;

[TestCategory("Draft7")]
[TestClass]
public class SuiteExclusiveMaximumValidation
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
    public void TestBelowTheExclusiveMaximumIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2.2");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBoundaryPointIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3.0");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAboveTheExclusiveMaximumIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3.5");
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
                "tests\\draft7\\exclusiveMaximum.json",
                "{\r\n            \"exclusiveMaximum\": 3.0\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.ExclusiveMaximum",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
