using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.Uri;

[TestCategory("Draft7")]
[TestClass]
public class SuiteValidationOfUrIs
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
    public void TestAValidUrlWithAnchorTag()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://foo.bar/?baz=qux#quux\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUrlWithAnchorTagAndParentheses()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://foo.com/blah_(wikipedia)_blah#cite-1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUrlWithUrlEncodedStuff()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://foo.bar/?q=Test%20URL-encoded%20stuff\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidPunyCodedUrl()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://xn--nw2a.xn--j6w193g/\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUrlWithManySpecialCharacters()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://-.~_!$&'()*+,;=:%40:80%2f::::::@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUrlBasedOnIPv4()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://223.255.255.254\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUrlWithFtpScheme()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"ftp://ftp.is.co.za/rfc/rfc1808.txt\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUrlForASimpleTextFile()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://www.ietf.org/rfc/rfc2396.txt\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUrl()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"ldap://[2001:db8::7]/c=GB?objectClass?one\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidMailtoUri()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"mailto:John.Doe@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidNewsgroupUri()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"news:comp.infosystems.www.servers.unix\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidTelUri()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"tel:+1-816-555-1212\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUrn()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"urn:oasis:names:specification:docbook:dtd:xml:4.1.2\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidProtocolRelativeUriReference()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"//foo.bar/?baz=qux#quux\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidRelativeUriReference()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/abc\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidUri()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\\\\\\WINDOWS\\\\fileshare\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidUriThoughValidUriReference()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"abc\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidUriWithSpaces()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http:// shouldfail.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidUriWithSpacesAndMissingScheme()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\":// should fail\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidUriWithCommaInScheme()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"bar,baz:foo\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidUserinfo()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"https://[@example.org/test.txt\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnescapedNonUsAsciiCharacters()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"https://example.org/foobar®.txt\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidBackslashCharacter()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"https://example.org/foobar\\\\.txt\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidCharacter()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"https://example.org/foobar\\\".txt\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidCharacters()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"https://example.org/foobar<>.txt\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidCharacters1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"https://example.org/foobar{}.txt\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidCharacter1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"https://example.org/foobar^.txt\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidCharacter2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"https://example.org/foobar`.txt\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidSpaceCharacter()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"https://example.org/foo bar.txt\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidCharacter3()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"https://example.org/foobar|.txt\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUriWithLeadingZeroIPv4IsStructurallyValidAsARegName()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://087.10.0.1/\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUriWithOutOfBoundsIPv4IsStructurallyValidAsARegName()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://999.999.999.999/\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidPercentEncodingWithNonHexDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://example.com/%6G\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIncompletePercentEncodingTriplet()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://example.com/%A\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLonePercentSignIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://example.com/%\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSchemeMustStartWithALetter()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1http://example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidCharacterInScheme()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"ht_tp://example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonNumericPortIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://example.com:abc/path\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft7/optional/format/uri.json",
                "{ \"format\": \"uri\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.Uri",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
