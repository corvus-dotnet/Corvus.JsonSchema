using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft4.FormatEmail;

[TestCategory("Draft4")]
[TestClass]
public class SuiteValidationOfNonStringEmailValues
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
                "tests/draft4/format-email.json",
                "{\n                \"format\": \"email\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.FormatEmail",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteValidationOfEMailAddresses
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
    public void TestAValidEMailAddress()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidEMailAddress()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2962\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTildeInLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"te~st@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTildeBeforeLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"~test@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTildeAfterLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test~@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAQuotedStringWithASpaceInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"joe bloggs\\\"@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAQuotedStringWithADoubleDotInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"joe..bloggs\\\"@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAQuotedStringWithAInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"joe@bloggs\\\"@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIPv4AddressLiteralAfterTheIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@[127.0.0.1]\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIPv6AddressLiteralAfterTheIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@[IPv6:::1]\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDotBeforeLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".test@example.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDotAfterLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test.@example.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoSeparatedDotsInsideLocalPartAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"te.s.t@example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoSubsequentDotsInsideLocalPartAreNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"te..st@example.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidDomain()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@invalid=domain.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidIPv4AddressLiteral()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@[127.0.0.300]\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoEmailAddressesIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"user1@oceania.org, user2@oceania.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFullFromHeaderIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"Winston Smith\\\" <winston.smith@recdep.minitrue> (Records Department)\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLocalPartIsRequired()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"@example.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDomainIsRequired()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe.bloggs@\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnquotedSpaceInLocalPartIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"joe bloggs@example.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADomainThatLooksLikeAnIPv4AddressWithoutBracketsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@255.255.255.255\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnEmptyQuotedStringInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"\\\"@iana.org\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAQuotedStringContainingOnlyASpaceInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\" \\\"@iana.org\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestConsecutiveHyphensInsideADomainLabelAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@c--n.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADomainLabelStartingWithADigitIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@123.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestANonAsciiCharacterInTheLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\u00e9@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestANonAsciiCharacterInTheDomainIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a@\\u00e9.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAFullwidthCommercialAtIsNotAValidSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\uff20iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestANonAsciiCharacterInAQuotedPairIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"test\\\\\\u00a9\\\"@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestATrailingLineFeedIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@iana.org\\n\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestATrailingCarriageReturnIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@iana.org\\r\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestATrailingSpaceIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a@iana.org \"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestALeadingSpaceIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\" a@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestATabInTheLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\tb@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADeleteCharacterInTheLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u007f@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAQuotedStringWithNoSpecialCharactersInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"test\\\"@iana.org\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAQuotedPairInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"\\\\a\\\"@iana.org\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnEscapedDoubleQuoteInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"\\\\\\\"\\\"@iana.org\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnEscapedBackslashInTheLocalPartIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"\\\\\\\\\\\"@iana.org\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnUnescapedDoubleQuoteInAnUnquotedLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test\\\"@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTextAfterTheClosingDoubleQuoteInTheLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"test\\\"test@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnUnclosedDoubleQuoteInTheLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\"test@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoUnquotedSignsAreNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a@b@c.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestABackslashEscapedInAnUnquotedLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test\\\\@test@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnAddressLiteralInTheLocalPartPositionIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"[1.2.3.4]@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnEmptyStringIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADomainLabelStartingWithAHyphenIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@-iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADomainLabelEndingWithAHyphenIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@iana-.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADomainStartingWithADotIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@.iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADomainEndingWithADotIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@iana.org.\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoSubsequentDotsInsideTheDomainAreNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@iana..com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestASingleLabelDomainIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@io\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnUnclosedAddressLiteralIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@[1.2.3.4\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEmptyBracketsAfterTheAreNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a@[]\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIPv4AddressLiteralWithThreeOctetsIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a@[1.2.3]\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIPv4AddressLiteralWithFiveOctetsIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a@[1.2.3.4.5]\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBracketedContentThatIsNotAnAddressIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@[RFC-5322-domain-literal]\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADomainLabelBeforeTheOpeningBracketIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test@a[255.255.255.255]\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestALowercaseIPv6TagInAnAddressLiteralIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a@[ipv6:::1]\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestACommaInAnUnquotedLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a,b@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestASemicolonInAnUnquotedLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a;b@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAColonInAnUnquotedLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a:b@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAParenthesisInAnUnquotedLocalPartIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"test(comment)@iana.org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAParenthesisInTheDomainIsNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a@b(c).org\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/format-email.json",
                "{\n                \"format\": \"email\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.FormatEmail",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
