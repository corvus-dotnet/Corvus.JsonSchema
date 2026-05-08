using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.UriTemplate;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteFormatUriTemplate
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
    public void TestAllStringFormatsIgnoreIntegers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreFloats()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("13.7");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreBooleans()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreNulls()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidUriTemplate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com/dictionary/{term:1}/{term}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidUriTemplate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com/dictionary/{term:1}/{term\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidUriTemplateWithoutVariables()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com/dictionary\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidRelativeUriTemplate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"dictionary/{term:1}/{term}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\uri-template.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"uri-template\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.UriTemplate",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
