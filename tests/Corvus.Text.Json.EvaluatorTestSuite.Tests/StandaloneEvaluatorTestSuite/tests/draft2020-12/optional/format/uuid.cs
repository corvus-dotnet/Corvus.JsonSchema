using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Uuid;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteUuidFormat
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
    public void TestAllUpperCase()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2EB8AA08-AA98-11EA-B4AA-73B441D16380\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllLowerCase()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08-aa98-11ea-b4aa-73b441d16380\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMixedCase()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08-AA98-11ea-B4Aa-73B441D16380\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllZeroesIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"00000000-0000-0000-0000-000000000000\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWrongLength()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08-aa98-11ea-b4aa-73b441d1638\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMissingSection()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08-aa98-11ea-73b441d16380\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBadCharactersNotHex()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08-aa98-11ea-b4ga-73b441d16380\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoDashes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08aa9811eab4aa73b441d16380\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTooFewDashes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08aa98-11ea-b4aa73b441d16380\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTooManyDashes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8-aa08-aa98-11ea-b4aa73b44-1d16380\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDashesInTheWrongSpot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa08aa9811eab4aa73b441d16380----\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestShiftedDashes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2eb8aa0-8aa98-11e-ab4aa7-3b441d16380\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidVersion4()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"98d80576-482e-427f-8434-7f86890ab222\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidVersion5()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"99c17cbb-656f-564a-940f-1a4568f03487\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHypotheticalVersion6()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"99c17cbb-656f-664a-940f-1a4568f03487\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHypotheticalVersion15()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"99c17cbb-656f-f64a-940f-1a4568f03487\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/optional/format/uuid.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"format\": \"uuid\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Uuid",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
