using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft4.Optional.Format.Hostname;

[TestCategory("Draft4")]
[TestClass]
public class SuiteValidationOfHostNames
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
    public void TestAValidHostName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"www.example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidPunycodedIdnHostname()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--4gbwdl.xn--wgbh1c\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHostnameWithConsecutiveHyphensRfc1123()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ab--cd.example\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAHostNameStartingWithAnIllegalCharacter()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-a-host-name-that-starts-with--\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAHostNameContainingIllegalCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"not_a_valid_host_name\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAHostNameWithAComponentTooLong()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a-vvvvvvvvvvvvvvvveeeeeeeeeeeeeeeerrrrrrrrrrrrrrrryyyyyyyyyyyyyyyy-long-host-name-component\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStartsWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-hostname\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEndsWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostname-\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStartsWithUnderscore()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"_hostname\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEndsWithUnderscore()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostname_\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestContainsUnderscore()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"host_name\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMaximumLabelLength()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExceedsMaximumLabelLength()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLabel()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostname\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLabelWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"host-name\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLabelWithDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"h0stn4me\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLabelEndingWithDigit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostnam3\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEmptyString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeadingDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".example\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example.\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIdnLabelSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example\\uff0ecom\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/optional/format/hostname.json",
                "{ \"format\": \"hostname\" }",
                "StandaloneEvaluatorTestSuite.Draft4.Optional.Format.Hostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
