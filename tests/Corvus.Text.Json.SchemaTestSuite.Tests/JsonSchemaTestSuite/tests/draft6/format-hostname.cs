using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft6.FormatHostname;

[TestCategory("Draft6")]
[TestClass]
public class SuiteValidationOfNonStringHostnameValues
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

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft6/format-hostname.json",
                "{\n                \"format\": \"hostname\"\n            }",
                "JsonSchemaTestSuite.Draft6.FormatHostname",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
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
    public void TestAValidHostName()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"www.example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLabel()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"hostname\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLabelWithDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"h0stn4me\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLabelStartingWithDigit()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1host\"");
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

    [TestMethod]
    public void TestSingleLabelWithHyphen()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"host-name\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
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
    public void TestContainsUnderscore()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"host_name\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceedsMaximumOverallLength256()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMaximumLabelLength63()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceedsMaximumLabelLength63()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingNewlineIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"example.com\\n\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingSpaceIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\" example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSpaceInsideALabelIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"ex ample.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNonAsciiKelvinSignU212a()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u212Aelvin.example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestConsecutiveHyphensInsideALabel()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a--b.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft6/format-hostname.json",
                "{\n                \"format\": \"hostname\"\n            }",
                "JsonSchemaTestSuite.Draft6.FormatHostname",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
