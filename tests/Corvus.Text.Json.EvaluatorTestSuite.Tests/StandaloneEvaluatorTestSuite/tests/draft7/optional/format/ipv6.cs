using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.Format.Ipv6;

[TestCategory("Draft7")]
[TestClass]
public class SuiteValidationOfIPv6Addresses
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
    public void TestAValidIPv6Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAGroupWith5HexDigitsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"12345::\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailing4HexSymbolsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::abef\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailing5HexSymbolsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::abcef\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIPv6AddressWithTooManyComponents()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:1:1:1:1:1:1:1:1:1:1:1:1:1:1:1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIPv6AddressContainingIllegalCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::laptop\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoDigitsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeadingColonsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::42:ff:1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingColonsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"d6::\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMissingLeadingOctetIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\":2:3:4:5:6:7:8\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMissingTrailingOctetIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2:3:4:5:6:7:\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMissingLeadingOctetWithOmittedOctetsLater()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\":2:3:4::8\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleSetOfDoubleColonsInTheMiddleIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:d6::42\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoSetsOfDoubleColonsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1::d6::42\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMixedFormatWithTheIpv4SectionAsDecimalOctets()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1::d6:192.168.0.1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMixedFormatWithDoubleColonsBetweenTheSections()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2::192.168.0.1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMixedFormatWithIpv4SectionWithOctetOutOfRange()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1::2:192.168.256.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMixedFormatWithIpv4SectionWithAHexOctet()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1::2:192.168.ff.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMixedFormatWithLeadingDoubleColonsIpv4MappedIpv6Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::ffff:192.168.0.1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTripleColonsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2:3:4:5:::8\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void Test8Octets()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2:3:4:5:6:7:8\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInsufficientOctetsWithoutDoubleColons()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2:3:4:5:6:7\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoColonsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIpv4IsNotIpv6()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"127.0.0.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIpv4SegmentMustHave4Octets()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2:3:4:1.2.3\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeadingWhitespaceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"  ::1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingWhitespaceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"::1  \"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNetmaskIsNotAPartOfIpv6Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"fe80::/64\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZoneIdIsNotAPartOfIpv6Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"fe80::a%eth1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestALongValidIpv6()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1000:1000:1000:1000:1000:1000:255.255.255.255\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestALongInvalidIpv6BelowLengthLimitFirst()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"100:100:100:100:100:100:255.255.255.255.255\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestALongInvalidIpv6BelowLengthLimitSecond()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"100:100:100:100:100:100:100:255.255.255.255\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidNonAscii৪ABengali4()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2:3:4:5:6:7:৪\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidNonAscii৪ABengali4InTheIPv4Portion()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1:2::192.16৪.0.1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/optional/format/ipv6.json",
                "{ \"format\": \"ipv6\" }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Format.Ipv6",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
