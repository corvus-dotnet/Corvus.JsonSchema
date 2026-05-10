using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft6.Optional.Format.DateTime;

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

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\format\\date-time.json",
                "{ \"format\": \"date-time\" }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.Format.DateTime",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
