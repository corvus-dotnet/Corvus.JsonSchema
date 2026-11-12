using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft7.FormatIdnEmail;

[TestCategory("Draft7")]
[TestClass]
public class SuiteValidationOfNonStringIdnEmailValues
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
                "tests/draft7/format-idn-email.json",
                "{\n                \"format\": \"idn-email\"\n            }",
                "JsonSchemaTestSuite.Draft7.FormatIdnEmail",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteValidationOfAnInternationalizedEMailAddresses
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
    public void TestAValidIdnEMailExampleExampleTestInHangul()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"실례@실례.테스트\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidEMailIdnEMailAddress()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2962\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidEMailAddress()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"joe.bloggs@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANonAsciiLocalPartWithAnAsciiDomainIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"δοκιμή@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANonAsciiQuotedLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"δοκιμή\\\"@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADomainLabelThatIsNotInUnicodeNfcIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"user@cafe\\u0301.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestALocalPartThatIsNotInUnicodeNfcIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"cafe\\u0301@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAC1ControlInTheLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0085@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANoncharacterInTheLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\uffff@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestALocalPartAtThe64OctetLimitIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAFullwidthCommercialAtIsNotALocalPartSeparator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"user\\uff20example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestALocalPartWithASupplementaryPlaneCharacterIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\ud835\\udd4f@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADomainLabelStartingWithAHyphenIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"δοκιμή@-example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADomainLabelEndingWithAHyphenIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"δοκιμή@example-.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADomainStartingWithADotIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"δοκιμή@.example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADomainEndingWithADotIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"δοκιμή@example.com.\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoSubsequentDotsInsideTheDomainAreNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"δοκιμή@example..com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDomainIsRequired()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"δοκιμή@\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft7/format-idn-email.json",
                "{\n                \"format\": \"idn-email\"\n            }",
                "JsonSchemaTestSuite.Draft7.FormatIdnEmail",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
