using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.Format.IdnHostname;

[TestCategory("Draft7")]
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
    public void TestAValidHostNameExampleTestInHangul()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"실례.테스트\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIllegalFirstCharU302eHangulSingleDotToneMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"〮실례.테스트\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestContainsIllegalCharU302eHangulSingleDotToneMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"실〮례.테스트\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAHostNameWithAComponentTooLong()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실례례테스트례례례례례례례례례례례례례례례례례테스트례례례례례례례례례례례례례례례례례례례테스트례례례례례례례례례례례례테스트례례실례.테스트\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLabelCorrectPunycode()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-> $1.00 <--\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidChinesePunycode()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--ihqwcrb4cv8a8dqg056pqjye\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidPunycode()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--X\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestULabelContainsInThe3rdAnd4thPosition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"XN--aa---o47jg78q\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestULabelStartsWithADash()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-hello\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestULabelEndsWithADash()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello-\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestULabelStartsAndEndsWithADash()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-hello-\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBeginsWithASpacingCombiningMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0903hello\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBeginsWithANonspacingMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0300hello\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBeginsWithAnEnclosingMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0488hello\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExceptionsThatArePvalidLeftToRightChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u00df\\u03c2\\u0f0b\\u3007\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExceptionsThatArePvalidRightToLeftChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u06fd\\u06fe\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExceptionsThatAreDisallowedRightToLeftChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0640\\u07fa\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExceptionsThatAreDisallowedLeftToRightChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u3031\\u3032\\u3033\\u3034\\u3035\\u302e\\u302f\\u303b\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMiddleDotWithNoPrecedingL()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\u00b7l\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMiddleDotWithNothingPreceding()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u00b7l\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMiddleDotWithNoFollowingL()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"l\\u00b7a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMiddleDotWithNothingFollowing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"l\\u00b7\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMiddleDotWithSurroundingLS()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"l\\u00b7l\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestGreekKeraiaNotFollowedByGreek()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u03b1\\u0375S\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestGreekKeraiaNotFollowedByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u03b1\\u0375\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestGreekKeraiaFollowedByGreek()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u03b1\\u0375\\u03b2\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHebrewGereshNotPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"A\\u05f3\\u05d1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHebrewGereshNotPrecededByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u05f3\\u05d1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHebrewGereshPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u05d0\\u05f3\\u05d1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHebrewGershayimNotPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"A\\u05f4\\u05d1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHebrewGershayimNotPrecededByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u05f4\\u05d1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHebrewGershayimPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u05d0\\u05f4\\u05d1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithNoHiraganaKatakanaOrHan()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"def\\u30fbabc\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithNoOtherCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u30fb\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithHiragana()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u30fb\\u3041\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithKatakana()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u30fb\\u30a1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithHan()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u30fb\\u4e08\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestArabicIndicDigitsMixedWithExtendedArabicIndicDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0628\\u0660\\u06f0\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestArabicIndicDigitsNotMixedWithExtendedArabicIndicDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0628\\u0660\\u0628\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExtendedArabicIndicDigitsNotMixedWithArabicIndicDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u06f00\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroWidthJoinerNotPrecededByVirama()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0915\\u200d\\u0937\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroWidthJoinerNotPrecededByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u200d\\u0937\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroWidthJoinerPrecededByVirama()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0915\\u094d\\u200d\\u0937\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroWidthNonJoinerPrecededByVirama()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0915\\u094d\\u200c\\u0937\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroWidthNonJoinerNotPrecededByViramaButMatchesRegexp()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0628\\u064a\\u200c\\u0628\\u064a\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLabel()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostname\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLabelWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"host-name\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLabelWithDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"h0stn4me\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLabelEndingWithDigit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostnam3\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEmptyString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\optional\\format\\idn-hostname.json",
                "{ \"format\": \"idn-hostname\" }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Format.IdnHostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u3002\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleFullwidthFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\uff0e\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleHalfwidthIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\uff61\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDotAsLabelSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a.b\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIdeographicFullStopAsLabelSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\u3002b\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFullwidthFullStopAsLabelSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\uff0eb\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHalfwidthIdeographicFullStopAsLabelSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\uff61b\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeadingDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".example\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeadingIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u3002example\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeadingFullwidthFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\uff0eexample\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeadingHalfwidthIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\uff61example\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example.\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example\\u3002\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingFullwidthFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example\\uff0e\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingHalfwidthIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example\\uff61\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLabelTooLongIfSeparatorIgnoredFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLabelTooLongIfSeparatorIgnoredIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα\\u3002com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLabelTooLongIfSeparatorIgnoredFullwidthFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα\\uff0ecom\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLabelTooLongIfSeparatorIgnoredHalfwidthIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα\\uff61com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\optional\\format\\idn-hostname.json",
                "{ \"format\": \"idn-hostname\" }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Format.IdnHostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
