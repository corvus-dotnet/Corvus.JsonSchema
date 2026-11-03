using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.FormatIri;

[TestCategory("Draft201909")]
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

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/format-iri.json",
                "{\n                \"format\": \"iri\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.FormatIri",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://2001:0db8:85a3:0000:0000:8a2e:0370:7334\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/format-iri.json",
                "{\n                \"format\": \"iri\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.FormatIri",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.ßår/?∂éœ=πîx#πîüx\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidIriWithAnchorTagAndParentheses()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.com/blah_(wîkïpédiå)_blah#ßité-1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidIriWithUrlEncodedStuff()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.ßår/?q=Test%20URL-encoded%20stuff\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidPercentEncodingWithNonHexDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.ßår/%6G\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIncompletePercentEncodingTriplet()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.ßår/%A\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLonePercentSignIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.ßår/%\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidIriWithManySpecialCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://-.~_!$&'()*+,;=:%40:80%2f::::::@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidIriBasedOnIPv6()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://[2001:0db8:85a3:0000:0000:8a2e:0370:7334]\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidIriBasedOnIPv6()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://2001:0db8:85a3:0000:0000:8a2e:0370:7334\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidRelativeIriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/abc\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidIri()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\\\\\WINDOWS\\\\filëßåré\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidIriThoughValidIriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"âππ\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIPv6HostWhoseEmbeddedIPv4HasALeadingZeroIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://[::ffff:192.168.0.01]\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidIriWithACompressedIPv6Host()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://[2001:db8::1]\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestATrailingNewlineAfterAValidIriIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.ßår/\\n\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidIriWithAnIPv4Host()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://192.168.0.1/p\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidIriWithNoAuthorityAndARootlessPath()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"urn:example:resource\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidIriWithNoAuthorityAndAnAbsolutePath()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"file:/etc/hosts\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidIriWithASupplementaryPlaneCharacterInThePath()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.ßår/𐌀\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidIriWithASupplementaryPlanePrivateUseCharInQuery()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.ßår/?q=󰀀\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/format-iri.json",
                "{\n                \"format\": \"iri\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.FormatIri",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
