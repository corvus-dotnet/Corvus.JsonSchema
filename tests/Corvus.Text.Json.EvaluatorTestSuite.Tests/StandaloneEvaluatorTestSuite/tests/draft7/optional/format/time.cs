using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.Format.Time;

[TestCategory("Draft7")]
[TestClass]
public class SuiteValidationOfTimeStrings
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
    public void TestAValidTimeString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06Z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidTimeStringWithExtraLeadingZeros()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"008:030:006Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidTimeStringWithNoLeadingZeroForSingleDigit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"8:3:6Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHourMinuteSecondMustBeTwoDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"8:0030:6Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidTimeStringWithLeapSecondZulu()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:59:60Z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondZuluWrongHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"22:59:60Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondZuluWrongMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:58:60Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidLeapSecondZeroTimeOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:59:60+00:00\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondZeroTimeOffsetWrongHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"22:59:60+00:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondZeroTimeOffsetWrongMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:58:60+00:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidLeapSecondPositiveTimeOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"01:29:60+01:30\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidLeapSecondLargePositiveTimeOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:29:60+23:30\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondPositiveTimeOffsetWrongHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:59:60+01:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondPositiveTimeOffsetWrongMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:59:60+00:30\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidLeapSecondNegativeTimeOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"15:59:60-08:00\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidLeapSecondLargeNegativeTimeOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"00:29:60-23:30\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondNegativeTimeOffsetWrongHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:59:60-01:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidLeapSecondNegativeTimeOffsetWrongMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:59:60-00:30\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidTimeStringWithSecondFraction()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:20:50.52Z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidTimeStringWithPreciseSecondFraction()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06.283185Z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidTimeStringWithPlusOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06+00:20\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidTimeStringWithMinusOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06-08:00\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTimeWithUnknownLocalOffsetIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"12:34:56-00:00\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHourMinuteInTimeOffsetMustBeTwoDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06-8:000\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidTimeStringWithCaseInsensitiveZ()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06z\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"24:00:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"00:60:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidSecond()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"00:00:61Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidLeapSecondWrongHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"22:59:60Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidLeapSecondWrongMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"23:58:60Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidTimeNumoffsetHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"01:02:03+24:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidTimeNumoffsetMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"01:02:03+00:60\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidTimeWithBothZAndNumoffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"01:02:03Z+00:30\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidOffsetIndicator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06 PST\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOnlyRfc3339NotAllOfIso8601AreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"01:01:01,1111\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoTimeOffset()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"12:00:00\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoTimeOffsetWithSecondFraction()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"12:00:00.52\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidNonAscii২ABengali2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1২:00:00Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOffsetNotStartingWithPlusOrMinus()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"08:30:06#00:20\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestContainsLetters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ab:cd:ef\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidTimeStringInDateTimeFormat()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2020-11-28T23:55:45Z\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/optional/format/time.json",
                "{ \"format\": \"time\" }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Format.Time",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
