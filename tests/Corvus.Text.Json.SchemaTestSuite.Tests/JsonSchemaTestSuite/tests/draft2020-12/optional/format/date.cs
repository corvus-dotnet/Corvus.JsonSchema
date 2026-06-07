using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Optional.Format.Date;

[TestCategory("Draft202012")]
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
    public void TestAValidDateString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1963-06-19\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInJanuary()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-01-31\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInJanuary()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-01-32\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith28DaysInFebruaryNormal()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2021-02-28\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAInvalidDateStringWith30DaysInFebruaryLeap()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-02-30\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInMarch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-03-31\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInMarch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-03-32\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith30DaysInApril()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-04-30\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAInvalidDateStringWith31DaysInApril()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-04-31\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInMay()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-05-31\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInMay()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-05-32\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith30DaysInJune()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-06-30\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAInvalidDateStringWith31DaysInJune()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-06-31\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInJuly()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-07-31\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInJuly()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-07-32\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInAugust()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-08-31\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInAugust()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-08-32\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith30DaysInSeptember()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-09-30\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAInvalidDateStringWith31DaysInSeptember()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-09-31\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInOctober()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-10-31\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInOctober()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-10-32\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith30DaysInNovember()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-11-30\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAInvalidDateStringWith31DaysInNovember()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-11-31\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith31DaysInDecember()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-12-31\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAInvalidDateStringWith32DaysInDecember()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-12-32\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidDateString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"06/19/1963\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOnlyRfc3339NotAllOfIso8601AreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2013-350\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonPaddedMonthDatesAreNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1998-1-20\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonPaddedDayDatesAreNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1998-01-1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidMonth()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1998-13-01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void Test2021IsNotALeapYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2021-02-29\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void Test2020IsALeapYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-02-29\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNonAscii৪ABengali4()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1963-06-1৪\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIso8601NonRfc3339YyyymmddWithoutDashes20230328()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"20230328\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIso8601NonRfc3339WeekNumberImplicitDayOfWeek20230102()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2023-W01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIso8601NonRfc3339WeekNumberWithDayOfWeek20230328()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2023-W13-2\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIso8601NonRfc3339WeekNumberRolloverToNextYear20230101()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2022W527\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidTimeStringInDateTimeFormat()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-11-28T23:55:45Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestYear0000IsALeapYear04000()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0000-02-29\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestCenturyYear0100IsNotALeapYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0100-02-29\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestCenturyYear0400IsALeapYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0400-02-29\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestCenturyYear2100IsNotALeapYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2100-02-29\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidLeadingWhitespaceIsNotPermitted()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\" 2024-01-15\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidTrailingWhitespaceIsNotPermitted()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2024-01-15 \"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidMonth00IsNotValidPerDateMonthRange0112()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2024-00-15\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidDay00IsNotValidPerDateMdayMinimumOf01()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2024-01-00\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidEmptyString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidEmbeddedWhitespaceBetweenYearAndMonth()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020 -01-01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidTrailingCharacterAfterValidFullDate()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-01-01X\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidTrailingZAfterFullDate()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-01-01Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidFullDateFollowedBySpaceAndTimeComponent()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-01-01 00:00:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidFourDigitYear0001()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0001-01-01\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidTwoDigitYearN2Digits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"20-01-01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidThreeDigitYearN1Digits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"998-01-01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidFiveDigitYearN1Digits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"12020-01-01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidPositiveSignPrefixOnYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"+2020-01-01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNegativeSignPrefixOnYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"-2020-01-01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNonAsciiBengaliDigitInYearField()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"২020-01-01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidAlphabeticCharactersInYearField()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"YYYY-01-01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/optional/format/date.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"format\": \"date\"\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.Date",
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
