using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Hostname;

[TestCategory("Draft201909")]
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
    public void TestAValidHostName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"www.example.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLabel()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostname\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLabelWithDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"h0stn4me\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLabelStartingWithDigit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1host\"");
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

    [TestMethod]
    public void TestSingleDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeadingDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".example\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example.\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIdnLabelSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example\\uff0ecom\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLabelWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"host-name\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStartsWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-hostname\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEndsWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostname-\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestContainsInThe3rdAnd4thPosition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"XN--aa---o47jg78q\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestContainsUnderscore()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"host_name\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExceedsMaximumOverallLength256()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMaximumLabelLength63()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExceedsMaximumLabelLength63()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl.com\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\hostname.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"hostname\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Hostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteValidationOfALabelPunycodeHostNames
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
    public void TestInvalidPunycode()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--X\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidHostNameExampleTestInHangul()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--9n2bp8q.xn--9t4b11yi5a\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestContainsIllegalCharU302eHangulSingleDotToneMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--07jt112bpxg.xn--9t4b11yi5a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBeginsWithASpacingCombiningMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--hello-txk\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBeginsWithANonspacingMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--hello-zed\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBeginsWithAnEnclosingMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--hello-6bf\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExceptionsThatArePvalidLeftToRightChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--zca29lwxobi7a\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExceptionsThatArePvalidRightToLeftChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--qmbc\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExceptionsThatAreDisallowedRightToLeftChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--chb89f\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExceptionsThatAreDisallowedLeftToRightChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--07jceefgh4c\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMiddleDotWithNoPrecedingL()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--al-0ea\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMiddleDotWithNothingPreceding()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--l-fda\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMiddleDotWithNoFollowingL()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--la-0ea\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMiddleDotWithNothingFollowing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--l-gda\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMiddleDotWithSurroundingLS()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--ll-0ea\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestGreekKeraiaNotFollowedByGreek()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--S-jib3p\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestGreekKeraiaNotFollowedByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--wva3j\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestGreekKeraiaFollowedByGreek()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--wva3je\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHebrewGereshNotPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--A-2hc5h\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHebrewGereshNotPrecededByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--5db1e\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHebrewGereshPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--4dbc5h\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHebrewGershayimNotPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--A-2hc8h\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHebrewGershayimNotPrecededByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--5db3e\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHebrewGershayimPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--4dbc8h\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithNoHiraganaKatakanaOrHan()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--defabc-k64e\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithNoOtherCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--vek\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithHiragana()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--k8j5u\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithKatakana()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--bck0j\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithHan()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--vek778f\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestArabicIndicDigitsMixedWithExtendedArabicIndicDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--ngb6iyr\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestArabicIndicDigitsNotMixedWithExtendedArabicIndicDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--ngba1o\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestExtendedArabicIndicDigitsNotMixedWithArabicIndicDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--0-gyc\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroWidthJoinerNotPrecededByVirama()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--11b2er09f\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroWidthJoinerNotPrecededByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--02b508i\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroWidthJoinerPrecededByVirama()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--11b2ezcw70k\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroWidthNonJoinerPrecededByVirama()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--11b2ezcs70k\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroWidthNonJoinerNotPrecededByViramaButMatchesRegexp()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--ngba5hb2804a\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\hostname.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"hostname\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Hostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
