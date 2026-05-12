using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.Hostname;

[TestCategory("Draft7")]
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
    public void TestAValidHostName()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"www.example.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLabel()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"hostname\"");
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

    [TestMethod]
    public void TestSingleDot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\".\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingDot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\".example\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingDot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"example.\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIdnLabelSeparator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"example\\uff0ecom\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLabelWithHyphen()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"host-name\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestStartsWithHyphen()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"-hostname\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEndsWithHyphen()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"hostname-\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestContainsInThe3rdAnd4thPosition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"XN--aa---o47jg78q\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestContainsUnderscore()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"host_name\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceedsMaximumOverallLength256()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMaximumLabelLength63()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceedsMaximumLabelLength63()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl.com\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\optional\\format\\hostname.json",
                "{ \"format\": \"hostname\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.Hostname",
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

[TestCategory("Draft7")]
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--X\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidHostNameExampleTestInHangul()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--9n2bp8q.xn--9t4b11yi5a\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestContainsIllegalCharU302eHangulSingleDotToneMark()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--07jt112bpxg.xn--9t4b11yi5a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBeginsWithASpacingCombiningMark()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--hello-txk\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBeginsWithANonspacingMark()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--hello-zed\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBeginsWithAnEnclosingMark()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--hello-6bf\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceptionsThatArePvalidLeftToRightChars()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--zca29lwxobi7a\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceptionsThatArePvalidRightToLeftChars()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--qmbc\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceptionsThatAreDisallowedRightToLeftChars()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--chb89f\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExceptionsThatAreDisallowedLeftToRightChars()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--07jceefgh4c\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMiddleDotWithNoPrecedingL()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--al-0ea\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMiddleDotWithNothingPreceding()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--l-fda\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMiddleDotWithNoFollowingL()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--la-0ea\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMiddleDotWithNothingFollowing()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--l-gda\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMiddleDotWithSurroundingLS()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--ll-0ea\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestGreekKeraiaNotFollowedByGreek()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--S-jib3p\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestGreekKeraiaNotFollowedByAnything()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--wva3j\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestGreekKeraiaFollowedByGreek()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--wva3je\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHebrewGereshNotPrecededByHebrew()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--A-2hc5h\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHebrewGereshNotPrecededByAnything()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--5db1e\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHebrewGereshPrecededByHebrew()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--4dbc5h\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHebrewGershayimNotPrecededByHebrew()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--A-2hc8h\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHebrewGershayimNotPrecededByAnything()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--5db3e\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHebrewGershayimPrecededByHebrew()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--4dbc8h\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithNoHiraganaKatakanaOrHan()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--defabc-k64e\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithNoOtherCharacters()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--vek\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithHiragana()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--k8j5u\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithKatakana()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--bck0j\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestKatakanaMiddleDotWithHan()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--vek778f\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestArabicIndicDigitsMixedWithExtendedArabicIndicDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--ngb6iyr\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestArabicIndicDigitsNotMixedWithExtendedArabicIndicDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--ngba1o\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExtendedArabicIndicDigitsNotMixedWithArabicIndicDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--0-gyc\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroWidthJoinerNotPrecededByVirama()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--11b2er09f\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroWidthJoinerNotPrecededByAnything()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--02b508i\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroWidthJoinerPrecededByVirama()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--11b2ezcw70k\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroWidthNonJoinerPrecededByVirama()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--11b2ezcs70k\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroWidthNonJoinerNotPrecededByViramaButMatchesRegexp()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"xn--ngba5hb2804a\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\optional\\format\\hostname.json",
                "{ \"format\": \"hostname\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.Hostname",
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
