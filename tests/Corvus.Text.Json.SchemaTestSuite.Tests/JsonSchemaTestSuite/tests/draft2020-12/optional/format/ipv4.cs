using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Optional.Format.Ipv4;

[TestCategory("Draft202012")]
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
    public void TestAValidIpAddress()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"192.168.0.1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIpAddressWithTooManyComponents()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"127.0.0.0.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIpAddressWithOutOfRangeValues()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"256.256.256.256\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIpAddressWithout4Components()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"127.0\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIpAddressAsAnInteger()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0x7f000001\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIpAddressAsAnIntegerDecimal()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2130706433\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNonAscii২ABengali2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1২7.0.0.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNetmaskIsNotAPartOfIpv4Address()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"192.168.1.0/24\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingWhitespaceIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\" 192.168.0.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingWhitespaceIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"192.168.0.1 \"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingNewlineIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"192.168.0.1\\n\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHexadecimalNotationIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0x7f.0.0.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOctalNotationExplicitIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0o10.0.0.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmptyPartDoubleDotIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"192.168..1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingDotIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\".192.168.0.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingDotIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"192.168.0.1.\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMinimumValidIPv4Address()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0.0.0.0\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMaximumValidIPv4Address()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"255.255.255.255\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmptyStringIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPlusSignIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"+1.2.3.4\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNegativeSignIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"-1.2.3.4\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExponentialNotationIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1e2.0.0.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAlphaCharactersAreInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"192.168.a.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInternalWhitespaceIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"192. 168.0.1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTabCharacterIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"192.168.0.1\\t\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithPortNumberIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"192.168.0.1:80\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleOctetOutOfRangeInLastPosition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"192.168.0.256\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/optional/format/ipv4.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"format\": \"ipv4\"\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.Ipv4",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
