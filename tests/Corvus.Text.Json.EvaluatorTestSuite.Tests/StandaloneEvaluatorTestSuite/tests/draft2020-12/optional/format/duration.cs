using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Duration;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidationOfDurationStrings
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
    public void TestAValidDurationString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P4DT12H30M5S\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidDurationString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT1D\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMustStartWithP()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"4DT12H30M5S\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoElementsPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoTimeElementsPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1YT\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoDateOrTimeElementsPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestElementsOutOfOrder()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P2D1Y\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMissingTimeSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1D2H\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTimeElementInTheDatePosition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P2S\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFourYearsDuration()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P4Y\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroTimeInSeconds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT0S\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroTimeInDays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P0D\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOneMonthDuration()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1M\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOneMinuteDuration()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT1M\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOneAndAHalfDaysInHours()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT36H\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOneAndAHalfDaysInDaysAndHours()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1DT12H\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoWeeks()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P2W\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWeeksCannotBeCombinedWithOtherUnits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1Y2W\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidNonAscii২ABengali2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P২Y\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestElementWithoutUnit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllDateAndTimeComponents()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1Y2M3DT4H5M6S\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDateComponentsOnly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1Y2M3D\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTimeComponentsOnly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT1H2M3S\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMonthAndDay()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1M2D\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHourAndMinute()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT1H30M\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMultiDigitValuesInAllComponents()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P10Y10M10DT10H10M10S\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFractionalDurationIsNotAllowedByRfc3339Abnf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT0.5S\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeadingWhitespaceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\" P1D\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTrailingWhitespaceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1D \"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEmptyStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestYearsAndMonthsCanAppearWithoutDays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1Y2M\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestYearsAndDaysCannotAppearWithoutMonths()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1Y2D\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMonthsAndDaysCanAppearWithoutYears()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"P1M2D\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHoursAndMinutesCanAppearWithoutSeconds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT1H2M\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestHoursAndSecondsCannotAppearWithoutMinutes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT1H2S\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMinutesAndSecondsCanAppearWithoutHour()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"PT1M2S\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/optional/format/duration.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"format\": \"duration\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Duration",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
