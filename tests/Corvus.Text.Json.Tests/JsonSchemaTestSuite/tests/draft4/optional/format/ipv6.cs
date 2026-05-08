using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft4.Optional.Format.Ipv6;

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreIntegers()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("12");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreFloats()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("13.7");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreObjects()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreArrays()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreBooleans()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("false");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreNulls()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("null");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidIPv6Address()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"::1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAGroupWith5HexDigitsIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"12345::\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailing4HexSymbolsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"::abef\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailing5HexSymbolsIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"::abcef\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIPv6AddressWithTooManyComponents()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1:1:1:1:1:1:1:1:1:1:1:1:1:1:1:1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIPv6AddressContainingIllegalCharacters()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"::laptop\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoDigitsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"::\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingColonsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"::42:ff:1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingColonsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"d6::\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMissingLeadingOctetIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\":2:3:4:5:6:7:8\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMissingTrailingOctetIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1:2:3:4:5:6:7:\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMissingLeadingOctetWithOmittedOctetsLater()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\":2:3:4::8\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleSetOfDoubleColonsInTheMiddleIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1:d6::42\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoSetsOfDoubleColonsIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1::d6::42\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMixedFormatWithTheIpv4SectionAsDecimalOctets()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1::d6:192.168.0.1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMixedFormatWithDoubleColonsBetweenTheSections()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1:2::192.168.0.1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMixedFormatWithIpv4SectionWithOctetOutOfRange()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1::2:192.168.256.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMixedFormatWithIpv4SectionWithAHexOctet()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1::2:192.168.ff.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMixedFormatWithLeadingDoubleColonsIpv4MappedIpv6Address()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"::ffff:192.168.0.1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTripleColonsIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1:2:3:4:5:::8\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void Test8Octets()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1:2:3:4:5:6:7:8\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInsufficientOctetsWithoutDoubleColons()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1:2:3:4:5:6:7\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoColonsIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIpv4IsNotIpv6()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"127.0.0.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIpv4SegmentMustHave4Octets()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1:2:3:4:1.2.3\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingWhitespaceIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"  ::1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingWhitespaceIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"::1  \"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNetmaskIsNotAPartOfIpv6Address()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"fe80::/64\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZoneIdIsNotAPartOfIpv6Address()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"fe80::a%eth1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestALongValidIpv6()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1000:1000:1000:1000:1000:1000:255.255.255.255\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestALongInvalidIpv6BelowLengthLimitFirst()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"100:100:100:100:100:100:255.255.255.255.255\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestALongInvalidIpv6BelowLengthLimitSecond()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"100:100:100:100:100:100:100:255.255.255.255\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNonAscii৪ABengali4()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1:2:3:4:5:6:7:৪\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNonAscii৪ABengali4InTheIPv4Portion()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1:2::192.16৪.0.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\format\\ipv6.json",
                "{ \"format\": \"ipv6\" }",
                "JsonSchemaTestSuite.Draft4.Optional.Format.Ipv6",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
