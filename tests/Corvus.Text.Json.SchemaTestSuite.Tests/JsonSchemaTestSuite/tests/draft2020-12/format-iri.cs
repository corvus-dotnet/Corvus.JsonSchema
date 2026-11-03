using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.FormatIri;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidationOfNonStringIriValues
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
                "tests/draft2020-12/format-iri.json",
                "{\n                \"format\": \"iri\"\n            }",
                "JsonSchemaTestSuite.Draft202012.FormatIri",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteIriFormat
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
    public void TestInvalidIriStringIsOnlyAnAnnotationByDefault()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://2001:0db8:85a3:0000:0000:8a2e:0370:7334\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/format-iri.json",
                "{\n                \"format\": \"iri\"\n            }",
                "JsonSchemaTestSuite.Draft202012.FormatIri",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidationOfIrIs
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
    public void TestAValidIriWithAnchorTag()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://ƒøø.ßår/?∂éœ=πîx#πîüx\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidIriWithAnchorTagAndParentheses()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://ƒøø.com/blah_(wîkïpédiå)_blah#ßité-1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidIriWithUrlEncodedStuff()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://ƒøø.ßår/?q=Test%20URL-encoded%20stuff\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidPercentEncodingWithNonHexDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://ƒøø.ßår/%6G\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIncompletePercentEncodingTriplet()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://ƒøø.ßår/%A\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLonePercentSignIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://ƒøø.ßår/%\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidIriWithManySpecialCharacters()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://-.~_!$&'()*+,;=:%40:80%2f::::::@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidIriBasedOnIPv6()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://[2001:0db8:85a3:0000:0000:8a2e:0370:7334]\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidIriBasedOnIPv6()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://2001:0db8:85a3:0000:0000:8a2e:0370:7334\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidRelativeIriReference()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/abc\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidIri()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\\\\\\WINDOWS\\\\filëßåré\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidIriThoughValidIriReference()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"âππ\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIPv6HostWhoseEmbeddedIPv4HasALeadingZeroIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://[::ffff:192.168.0.01]\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidIriWithACompressedIPv6Host()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://[2001:db8::1]\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestATrailingNewlineAfterAValidIriIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://ƒøø.ßår/\\n\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidIriWithAnIPv4Host()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://192.168.0.1/p\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidIriWithNoAuthorityAndARootlessPath()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"urn:example:resource\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidIriWithNoAuthorityAndAnAbsolutePath()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"file:/etc/hosts\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidIriWithASupplementaryPlaneCharacterInThePath()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://ƒøø.ßår/𐌀\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidIriWithASupplementaryPlanePrivateUseCharInQuery()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://ƒøø.ßår/?q=󰀀\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/format-iri.json",
                "{\n                \"format\": \"iri\"\n            }",
                "JsonSchemaTestSuite.Draft202012.FormatIri",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
