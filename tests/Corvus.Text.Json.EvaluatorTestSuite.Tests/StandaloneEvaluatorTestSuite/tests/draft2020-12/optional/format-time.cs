using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.FormatTime;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidationOfTimeStrings
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
    public void TestInvalidLeapSecondZuluWrongHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"22:59:60Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondZuluWrongMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:58:60Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondZeroTimeOffsetWrongHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"22:59:60+00:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondZeroTimeOffsetWrongMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:58:60+00:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondPositiveTimeOffsetWrongHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:59:60+01:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondPositiveTimeOffsetWrongMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:59:60+00:30\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondNegativeTimeOffsetWrongHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:59:60-01:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondNegativeTimeOffsetWrongMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:59:60-00:30\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/optional/format-time.json",
                "{\n                \"format\": \"time\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.FormatTime",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
