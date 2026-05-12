using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.FormatAssertion;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionFalse
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
    public void TestFormatAssertionFalseValidString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"127.0.0.1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFormatAssertionFalseInvalidString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"not-an-ipv4\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format-assertion.json",
                "{\r\n            \"$id\": \"https://schema/using/format-assertion/false\",\r\n            \"$schema\": \"http://localhost:1234/draft2020-12/format-assertion-false.json\",\r\n            \"format\": \"ipv4\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.FormatAssertion",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionTrue
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
    public void TestFormatAssertionTrueValidString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"127.0.0.1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFormatAssertionTrueInvalidString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"not-an-ipv4\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format-assertion.json",
                "{\r\n            \"$id\": \"https://schema/using/format-assertion/true\",\r\n            \"$schema\": \"http://localhost:1234/draft2020-12/format-assertion-true.json\",\r\n            \"format\": \"ipv4\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.FormatAssertion",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
