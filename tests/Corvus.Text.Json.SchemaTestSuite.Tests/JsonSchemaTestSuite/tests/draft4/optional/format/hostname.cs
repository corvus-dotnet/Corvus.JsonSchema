using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft4.Optional.Format.Hostname;

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
    public void TestAValidHostName()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"www.example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidPunycodedIdnHostname()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--4gbwdl.xn--wgbh1c\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHostnameWithConsecutiveHyphensRfc1123()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"ab--cd.example\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAHostNameStartingWithAnIllegalCharacter()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"-a-host-name-that-starts-with--\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAHostNameContainingIllegalCharacters()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"not_a_valid_host_name\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAHostNameWithAComponentTooLong()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a-vvvvvvvvvvvvvvvveeeeeeeeeeeeeeeerrrrrrrrrrrrrrrryyyyyyyyyyyyyyyy-long-host-name-component\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestStartsWithHyphen()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"-hostname\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEndsWithHyphen()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"hostname-\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestStartsWithUnderscore()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"_hostname\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEndsWithUnderscore()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"hostname_\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestContainsUnderscore()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"host_name\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMaximumLabelLength()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceedsMaximumLabelLength()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLabel()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"hostname\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLabelWithHyphen()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"host-name\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLabelWithDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"h0stn4me\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLabelEndingWithDigit()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"hostnam3\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmptyString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleDot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\".\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingDot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\".example\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingDot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"example.\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIdnLabelSeparator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"example\\uff0ecom\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft4/optional/format/hostname.json",
                "{ \"format\": \"hostname\" }",
                "JsonSchemaTestSuite.Draft4.Optional.Format.Hostname",
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
