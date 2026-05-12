using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Email;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidationOfEMailAddresses
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
    public void TestAValidEMailAddress()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidEMailAddress()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2962\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTildeInLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"te~st@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTildeBeforeLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"~test@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTildeAfterLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test~@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAQuotedStringWithASpaceInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"joe bloggs\\\"@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAQuotedStringWithADoubleDotInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"joe..bloggs\\\"@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAQuotedStringWithAInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"joe@bloggs\\\"@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIPv4AddressLiteralAfterTheIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@[127.0.0.1]\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIPv6AddressLiteralAfterTheIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@[IPv6:::1]\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDotBeforeLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".test@example.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDotAfterLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test.@example.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoSeparatedDotsInsideLocalPartAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"te.s.t@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoSubsequentDotsInsideLocalPartAreNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"te..st@example.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidDomain()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@invalid=domain.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidIPv4AddressLiteral()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@[127.0.0.300]\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoEmailAddressesIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"user1@oceania.org, user2@oceania.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFullFromHeaderIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"Winston Smith\\\" <winston.smith@recdep.minitrue> (Records Department)\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLocalPartIsRequired()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"@example.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDomainIsRequired()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnquotedSpaceInLocalPartIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe bloggs@example.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format\\email.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"email\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Email",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
