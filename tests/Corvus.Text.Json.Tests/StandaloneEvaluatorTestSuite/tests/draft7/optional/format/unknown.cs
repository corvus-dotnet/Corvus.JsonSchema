using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.Format.Unknown;

[TestCategory("Draft7")]
[TestClass]
public class SuiteUnknownFormat
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
    public void TestUnknownFormatsIgnoreIntegers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnknownFormatsIgnoreFloats()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("13.7");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnknownFormatsIgnoreObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnknownFormatsIgnoreArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnknownFormatsIgnoreBooleans()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnknownFormatsIgnoreNulls()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnknownFormatsIgnoreStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"string\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\optional\\format\\unknown.json",
                "{ \"format\": \"unknown\" }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Format.Unknown",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
