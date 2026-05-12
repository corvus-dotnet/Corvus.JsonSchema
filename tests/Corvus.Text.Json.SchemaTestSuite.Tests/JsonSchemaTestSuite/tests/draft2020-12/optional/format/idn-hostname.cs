using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Optional.Format.IdnHostname;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidationOfInternationalizedHostNames
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
        (s_fixture as IDisposable)?.Dispose();
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
    public void TestAValidHostNameExampleTestInHangul()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"실례.테스트\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIllegalFirstCharU302eHangulSingleDotToneMark()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"〮실례.테스트\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestContainsIllegalCharU302eHangulSingleDotToneMark()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"실〮례.테스트\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAHostNameWithAComponentTooLong()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실례례테스트례례례례례례례례례례례례례례례례례테스트례례례례례례례례례례례례례례례례례례례테스트례례례례례례례례례례례례테스트례례실례.테스트\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidLabelCorrectPunycode()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"-> $1.00 <--\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidChinesePunycode()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--ihqwcrb4cv8a8dqg056pqjye\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidPunycode()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--X\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestULabelContainsInThe3rdAnd4thPosition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"XN--aa---o47jg78q\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestULabelStartsWithADash()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"-hello\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestULabelEndsWithADash()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"hello-\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestULabelStartsAndEndsWithADash()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"-hello-\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBeginsWithASpacingCombiningMark()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0903hello\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBeginsWithANonspacingMark()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0300hello\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBeginsWithAnEnclosingMark()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0488hello\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceptionsThatArePvalidLeftToRightChars()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u00df\\u03c2\\u0f0b\\u3007\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceptionsThatArePvalidRightToLeftChars()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u06fd\\u06fe\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceptionsThatAreDisallowedRightToLeftChars()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0640\\u07fa\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceptionsThatAreDisallowedLeftToRightChars()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u3031\\u3032\\u3033\\u3034\\u3035\\u302e\\u302f\\u303b\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMiddleDotWithNoPrecedingL()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\\u00b7l\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMiddleDotWithNothingPreceding()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u00b7l\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMiddleDotWithNoFollowingL()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"l\\u00b7a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMiddleDotWithNothingFollowing()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"l\\u00b7\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMiddleDotWithSurroundingLS()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"l\\u00b7l\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestGreekKeraiaNotFollowedByGreek()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u03b1\\u0375S\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestGreekKeraiaNotFollowedByAnything()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u03b1\\u0375\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestGreekKeraiaFollowedByGreek()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u03b1\\u0375\\u03b2\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHebrewGereshNotPrecededByHebrew()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"A\\u05f3\\u05d1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHebrewGereshNotPrecededByAnything()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u05f3\\u05d1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHebrewGereshPrecededByHebrew()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u05d0\\u05f3\\u05d1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHebrewGershayimNotPrecededByHebrew()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"A\\u05f4\\u05d1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHebrewGershayimNotPrecededByAnything()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u05f4\\u05d1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHebrewGershayimPrecededByHebrew()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u05d0\\u05f4\\u05d1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithNoHiraganaKatakanaOrHan()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"def\\u30fbabc\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithNoOtherCharacters()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u30fb\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithHiragana()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u30fb\\u3041\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithKatakana()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u30fb\\u30a1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithHan()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u30fb\\u4e08\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestArabicIndicDigitsMixedWithExtendedArabicIndicDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0628\\u0660\\u06f0\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestArabicIndicDigitsNotMixedWithExtendedArabicIndicDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0628\\u0660\\u0628\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExtendedArabicIndicDigitsNotMixedWithArabicIndicDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u06f00\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroWidthJoinerNotPrecededByVirama()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0915\\u200d\\u0937\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroWidthJoinerNotPrecededByAnything()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u200d\\u0937\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroWidthJoinerPrecededByVirama()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0915\\u094d\\u200d\\u0937\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroWidthNonJoinerPrecededByVirama()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0915\\u094d\\u200c\\u0937\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroWidthNonJoinerNotPrecededByViramaButMatchesRegexp()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0628\\u064a\\u200c\\u0628\\u064a\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
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

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format\\idn-hostname.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"idn-hostname\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.IdnHostname",
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

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidationOfSeparatorsInInternationalizedHostNames
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestSingleDot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\".\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleIdeographicFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u3002\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleFullwidthFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\uff0e\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleHalfwidthIdeographicFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\uff61\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDotAsLabelSeparator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a.b\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIdeographicFullStopAsLabelSeparator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\\u3002b\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFullwidthFullStopAsLabelSeparator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\\uff0eb\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHalfwidthIdeographicFullStopAsLabelSeparator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\\uff61b\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingDot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\".example\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingIdeographicFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u3002example\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingFullwidthFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\uff0eexample\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingHalfwidthIdeographicFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\uff61example\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingDot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"example.\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingIdeographicFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"example\\u3002\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingFullwidthFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"example\\uff0e\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingHalfwidthIdeographicFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"example\\uff61\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLabelTooLongIfSeparatorIgnoredFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLabelTooLongIfSeparatorIgnoredIdeographicFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα\\u3002com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLabelTooLongIfSeparatorIgnoredFullwidthFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα\\uff0ecom\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLabelTooLongIfSeparatorIgnoredHalfwidthIdeographicFullStop()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα\\uff61com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format\\idn-hostname.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"idn-hostname\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.IdnHostname",
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
