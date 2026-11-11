using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft6.FormatDateTime;

[TestCategory("Draft6")]
[TestClass]
public class SuiteValidationOfNonStringDateTimeValues
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
                "tests/draft6/format-date-time.json",
                "{\n                \"format\": \"date-time\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.FormatDateTime",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteValidationOfDateTimeStrings
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
    public void TestAValidDateTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-19T08:30:06.283185Z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateTimeStringWithoutSecondFraction()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-19T08:30:06Z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateTimeStringWithPlusOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1937-01-01T12:00:27.87+00:20\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateTimeStringWithMinusOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1990-12-31T15:59:50.123-08:00\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateTimeWithALeapSecondUtc()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1998-12-31T23:59:60Z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateTimeWithALeapSecondWithMinusOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1998-12-31T15:59:60.123-08:00\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidDateTimePastLeapSecondUtc()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1998-12-31T23:59:61Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidDateTimeWithLeapSecondOnAWrongMinuteUtc()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1998-12-31T23:58:60Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidDateTimeWithLeapSecondOnAWrongHourUtc()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1998-12-31T22:59:60Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidDayInDateTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1990-02-31T15:59:59.123-08:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidOffsetInDateTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1990-12-31T15:59:59-24:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidClosingZAfterTimeZoneOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-19T08:30:06.28123+01:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidHourInDateTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1990-12-31T24:00:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidMinuteInDateTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1990-12-31T15:60:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidOffsetMinuteInDateTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1990-12-31T10:00:00+10:60\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidDateTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"06/19/1963 08:30:06 PST\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCaseInsensitiveTAndZ()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-19t08:30:06.283185z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOnlyRfc3339NotAllOfIso8601AreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2013-350T01:01:01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidNonPaddedMonthDates()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-6-19T08:30:06.283185Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidNonPaddedDayDates()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-1T08:30:06.283185Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidNonAscii৪ABengali4InDatePortion()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-1৪T00:00:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidNonAscii৪ABengali4InTimePortion()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1963-06-11T0৪:00:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidExtendedYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"+11963-06-19T08:30:06.283185Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestANumericOffsetWithoutMinutesIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1985-04-12T23:20:50+01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHour24IsInvalidEvenWithALeapSecond()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2016-12-31T24:59:60+01:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestASecondFractionOfFifteenNinesIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1985-04-12T00:59:59.999999999999999Z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestATrailingNewlineIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1985-04-12T23:20:50Z\\n\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidDateTimeStringWithoutSeconds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1985-04-12T23:20Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidDateTimeStringWithTrailingContentAfterTheOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1985-04-12T23:20:50Ztail\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidMinuteInDateTimeStringWithANumericOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1985-04-12T23:60:00+00:01\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidDateTimeStringWith28DaysInFebruaryNormal()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2021-02-28T00:00:00Z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidDateTimeStringWith30DaysInFebruaryLeap()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-02-30T00:00:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void Test2021IsNotALeapYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2021-02-29T00:00:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void Test2020IsALeapYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-02-29T00:00:00Z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCenturyYear0100IsNotALeapYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0100-02-29T00:00:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCenturyYear0400IsALeapYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0400-02-29T00:00:00Z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCenturyYear2100IsNotALeapYear()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2100-02-29T00:00:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestASecondFractionWithNoDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1985-04-12T23:20:50.Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestACommaDecimalSeparatorWithATimeOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1985-04-12T23:20:50,5Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeadingWhitespaceBeforeAValidDateTime()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\" 1985-04-12T23:20:50Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingWhitespaceAfterAValidDateTime()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1985-04-12T23:20:50Z \"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/format-date-time.json",
                "{\n                \"format\": \"date-time\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.FormatDateTime",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
