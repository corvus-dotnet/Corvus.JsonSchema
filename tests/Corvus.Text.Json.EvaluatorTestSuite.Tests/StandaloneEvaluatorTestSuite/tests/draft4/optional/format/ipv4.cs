using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft4.Optional.Format.Ipv4;

[TestCategory("Draft4")]
[TestClass]
public class SuiteValidationOfIpAddresses
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
    public void TestAValidIpAddress()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIpAddressWithTooManyComponents()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"127.0.0.0.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIpAddressWithOutOfRangeValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"256.256.256.256\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIpAddressWithout4Components()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"127.0\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIpAddressAsAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0x7f000001\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIpAddressAsAnIntegerDecimal()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2130706433\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidNonAscii২ABengali2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1২7.0.0.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNetmaskIsNotAPartOfIpv4Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.1.0/24\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeadingWhitespaceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\" 192.168.0.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingWhitespaceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1 \"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingNewlineIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1\\n\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHexadecimalNotationIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0x7f.0.0.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOctalNotationExplicitIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0o10.0.0.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEmptyPartDoubleDotIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168..1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeadingDotIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".192.168.0.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingDotIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1.\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMinimumValidIPv4Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0.0.0.0\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMaximumValidIPv4Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"255.255.255.255\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEmptyStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPlusSignIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"+1.2.3.4\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNegativeSignIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-1.2.3.4\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExponentialNotationIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1e2.0.0.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAlphaCharactersAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.a.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInternalWhitespaceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192. 168.0.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTabCharacterIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1\\t\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithPortNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1:80\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleOctetOutOfRangeInLastPosition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.256\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/optional/format/ipv4.json",
                "{ \"format\": \"ipv4\" }",
                "StandaloneEvaluatorTestSuite.Draft4.Optional.Format.Ipv4",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
