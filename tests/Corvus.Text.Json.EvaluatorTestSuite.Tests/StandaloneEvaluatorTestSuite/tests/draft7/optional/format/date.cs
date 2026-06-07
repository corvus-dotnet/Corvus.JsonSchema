using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.Format.Date;

[TestCategory("Draft7")]
[TestClass]
public class SuiteValidationOfDateStrings
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
    public void TestAValidDateString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-19\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInJanuary()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-01-31\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInJanuary()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-01-32\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateStringWith28DaysInFebruaryNormal()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2021-02-28\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAInvalidDateStringWith30DaysInFebruaryLeap()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-02-30\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInMarch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-03-31\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInMarch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-03-32\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateStringWith30DaysInApril()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-04-30\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAInvalidDateStringWith31DaysInApril()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-04-31\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInMay()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-05-31\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInMay()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-05-32\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateStringWith30DaysInJune()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-06-30\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAInvalidDateStringWith31DaysInJune()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-06-31\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInJuly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-07-31\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInJuly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-07-32\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInAugust()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-08-31\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInAugust()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-08-32\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateStringWith30DaysInSeptember()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-09-30\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAInvalidDateStringWith31DaysInSeptember()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-09-31\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInOctober()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-10-31\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInOctober()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-10-32\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateStringWith30DaysInNovember()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-11-30\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAInvalidDateStringWith31DaysInNovember()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-11-31\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInDecember()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-12-31\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInDecember()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-12-32\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidDateString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"06/19/1963\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOnlyRfc3339NotAllOfIso8601AreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2013-350\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonPaddedMonthDatesAreNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1998-1-20\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonPaddedDayDatesAreNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1998-01-1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidMonth()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1998-13-01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void Test2021IsNotALeapYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2021-02-29\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void Test2020IsALeapYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-02-29\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidNonAscii৪ABengali4()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-1৪\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIso8601NonRfc3339YyyymmddWithoutDashes20230328()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"20230328\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIso8601NonRfc3339WeekNumberImplicitDayOfWeek20230102()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2023-W01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIso8601NonRfc3339WeekNumberWithDayOfWeek20230328()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2023-W13-2\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIso8601NonRfc3339WeekNumberRolloverToNextYear20230101()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2022W527\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidTimeStringInDateTimeFormat()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-11-28T23:55:45Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestYear0000IsALeapYear04000()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0000-02-29\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCenturyYear0100IsNotALeapYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0100-02-29\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCenturyYear0400IsALeapYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0400-02-29\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCenturyYear2100IsNotALeapYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2100-02-29\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeadingWhitespaceIsNotPermitted()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\" 2024-01-15\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidTrailingWhitespaceIsNotPermitted()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2024-01-15 \"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidMonth00IsNotValidPerDateMonthRange0112()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2024-00-15\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidDay00IsNotValidPerDateMdayMinimumOf01()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2024-01-00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidEmptyString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidEmbeddedWhitespaceBetweenYearAndMonth()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020 -01-01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidTrailingCharacterAfterValidFullDate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-01-01X\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidTrailingZAfterFullDate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-01-01Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidFullDateFollowedBySpaceAndTimeComponent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-01-01 00:00:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidFourDigitYear0001()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0001-01-01\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidTwoDigitYearN2Digits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"20-01-01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidThreeDigitYearN1Digits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"998-01-01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidFiveDigitYearN1Digits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"12020-01-01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidPositiveSignPrefixOnYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"+2020-01-01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidNegativeSignPrefixOnYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-2020-01-01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidNonAsciiBengaliDigitInYearField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"২020-01-01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidAlphabeticCharactersInYearField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"YYYY-01-01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/optional/format/date.json",
                "{ \"format\": \"date\" }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Format.Date",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
