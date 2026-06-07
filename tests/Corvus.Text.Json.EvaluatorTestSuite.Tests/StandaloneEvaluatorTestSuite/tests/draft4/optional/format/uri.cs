using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft4.Optional.Format.Uri;

[TestCategory("Draft4")]
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
    public void TestAValidUrlWithAnchorTag()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://foo.bar/?baz=qux#quux\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidUrlWithAnchorTagAndParentheses()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://foo.com/blah_(wikipedia)_blah#cite-1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidUrlWithUrlEncodedStuff()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://foo.bar/?q=Test%20URL-encoded%20stuff\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidPunyCodedUrl()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://xn--nw2a.xn--j6w193g/\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidUrlWithManySpecialCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://-.~_!$&'()*+,;=:%40:80%2f::::::@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidUrlBasedOnIPv4()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://223.255.255.254\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidUrlWithFtpScheme()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ftp://ftp.is.co.za/rfc/rfc1808.txt\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidUrlForASimpleTextFile()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://www.ietf.org/rfc/rfc2396.txt\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidUrl()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ldap://[2001:db8::7]/c=GB?objectClass?one\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidMailtoUri()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"mailto:John.Doe@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidNewsgroupUri()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"news:comp.infosystems.www.servers.unix\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidTelUri()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"tel:+1-816-555-1212\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidUrn()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"urn:oasis:names:specification:docbook:dtd:xml:4.1.2\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidProtocolRelativeUriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"//foo.bar/?baz=qux#quux\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidRelativeUriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/abc\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidUri()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\\\\\WINDOWS\\\\fileshare\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidUriThoughValidUriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abc\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidUriWithSpaces()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http:// shouldfail.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidUriWithSpacesAndMissingScheme()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\":// should fail\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidUriWithCommaInScheme()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"bar,baz:foo\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidUserinfo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://[@example.org/test.txt\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnescapedNonUsAsciiCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar®.txt\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidBackslashCharacter()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar\\\\.txt\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidCharacter()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar\\\".txt\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar<>.txt\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidCharacters1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar{}.txt\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidCharacter1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar^.txt\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidCharacter2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar`.txt\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidSpaceCharacter()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foo bar.txt\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidCharacter3()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar|.txt\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUriWithLeadingZeroIPv4IsStructurallyValidAsARegName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://087.10.0.1/\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUriWithOutOfBoundsIPv4IsStructurallyValidAsARegName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://999.999.999.999/\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidPercentEncodingWithNonHexDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com/%6G\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIncompletePercentEncodingTriplet()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com/%A\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLonePercentSignIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com/%\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSchemeMustStartWithALetter()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1http://example.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidCharacterInScheme()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ht_tp://example.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonNumericPortIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com:abc/path\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/optional/format/uri.json",
                "{ \"format\": \"uri\" }",
                "StandaloneEvaluatorTestSuite.Draft4.Optional.Format.Uri",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
