using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft6.FormatEmail;

[TestCategory("Draft6")]
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
                "tests/draft6/format-email.json",
                "{\n                \"format\": \"email\"\n            }",
                "JsonSchemaTestSuite.Draft6.FormatEmail",
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"joe.bloggs@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidEMailAddress()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2962\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTildeInLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"te~st@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTildeBeforeLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"~test@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTildeAfterLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test~@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAQuotedStringWithASpaceInTheLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"joe bloggs\\\"@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAQuotedStringWithADoubleDotInTheLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"joe..bloggs\\\"@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAQuotedStringWithAInTheLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"joe@bloggs\\\"@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIPv4AddressLiteralAfterTheIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"joe.bloggs@[127.0.0.1]\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIPv6AddressLiteralAfterTheIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"joe.bloggs@[IPv6:::1]\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDotBeforeLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\".test@example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDotAfterLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test.@example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoSeparatedDotsInsideLocalPartAreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"te.s.t@example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoSubsequentDotsInsideLocalPartAreNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"te..st@example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidDomain()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"joe.bloggs@invalid=domain.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidIPv4AddressLiteral()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"joe.bloggs@[127.0.0.300]\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoEmailAddressesIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"user1@oceania.org, user2@oceania.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFullFromHeaderIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"Winston Smith\\\" <winston.smith@recdep.minitrue> (Records Department)\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLocalPartIsRequired()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"@example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDomainIsRequired()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"joe.bloggs@\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnquotedSpaceInLocalPartIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"joe bloggs@example.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADomainThatLooksLikeAnIPv4AddressWithoutBracketsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@255.255.255.255\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnEmptyQuotedStringInTheLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"\\\"@iana.org\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAQuotedStringContainingOnlyASpaceInTheLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\" \\\"@iana.org\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestConsecutiveHyphensInsideADomainLabelAreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@c--n.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADomainLabelStartingWithADigitIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@123.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANonAsciiCharacterInTheLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\\u00e9@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANonAsciiCharacterInTheDomainIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a@\\u00e9.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAFullwidthCommercialAtIsNotAValidSeparator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\\uff20iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANonAsciiCharacterInAQuotedPairIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"test\\\\\\u00a9\\\"@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestATrailingLineFeedIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@iana.org\\n\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestATrailingCarriageReturnIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@iana.org\\r\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestATrailingSpaceIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a@iana.org \"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestALeadingSpaceIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\" a@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestATabInTheLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\\tb@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADeleteCharacterInTheLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u007f@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAQuotedStringWithNoSpecialCharactersInTheLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"test\\\"@iana.org\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAQuotedPairInTheLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"\\\\a\\\"@iana.org\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnEscapedDoubleQuoteInTheLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"\\\\\\\"\\\"@iana.org\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnEscapedBackslashInTheLocalPartIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"\\\\\\\\\\\"@iana.org\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnUnescapedDoubleQuoteInAnUnquotedLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test\\\"@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTextAfterTheClosingDoubleQuoteInTheLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"test\\\"test@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnUnclosedDoubleQuoteInTheLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\"test@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoUnquotedSignsAreNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a@b@c.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestABackslashEscapedInAnUnquotedLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test\\\\@test@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnAddressLiteralInTheLocalPartPositionIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"[1.2.3.4]@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnEmptyStringIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADomainLabelStartingWithAHyphenIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@-iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADomainLabelEndingWithAHyphenIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@iana-.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADomainStartingWithADotIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@.iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADomainEndingWithADotIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@iana.org.\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoSubsequentDotsInsideTheDomainAreNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@iana..com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestASingleLabelDomainIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@io\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnUnclosedAddressLiteralIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@[1.2.3.4\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmptyBracketsAfterTheAreNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a@[]\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIPv4AddressLiteralWithThreeOctetsIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a@[1.2.3]\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnIPv4AddressLiteralWithFiveOctetsIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a@[1.2.3.4.5]\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBracketedContentThatIsNotAnAddressIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@[RFC-5322-domain-literal]\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADomainLabelBeforeTheOpeningBracketIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test@a[255.255.255.255]\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestALowercaseIPv6TagInAnAddressLiteralIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a@[ipv6:::1]\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestACommaInAnUnquotedLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a,b@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestASemicolonInAnUnquotedLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a;b@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAColonInAnUnquotedLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a:b@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAParenthesisInAnUnquotedLocalPartIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"test(comment)@iana.org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAParenthesisInTheDomainIsNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a@b(c).org\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft6/format-email.json",
                "{\n                \"format\": \"email\"\n            }",
                "JsonSchemaTestSuite.Draft6.FormatEmail",
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
