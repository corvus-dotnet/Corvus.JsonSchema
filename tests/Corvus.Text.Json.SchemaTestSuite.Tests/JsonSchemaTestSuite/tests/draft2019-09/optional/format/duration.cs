using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft201909.Optional.Format.Duration;

[TestCategory("Draft201909")]
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
    public void TestAValidDurationString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P4DT12H30M5S\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidDurationString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"PT1D\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMustStartWithP()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"4DT12H30M5S\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoElementsPresent()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoTimeElementsPresent()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1YT\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoDateOrTimeElementsPresent()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"PT\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestElementsOutOfOrder()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P2D1Y\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMissingTimeSeparator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1D2H\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTimeElementInTheDatePosition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P2S\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFourYearsDuration()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P4Y\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroTimeInSeconds()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"PT0S\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroTimeInDays()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P0D\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOneMonthDuration()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1M\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOneMinuteDuration()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"PT1M\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOneAndAHalfDaysInHours()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"PT36H\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOneAndAHalfDaysInDaysAndHours()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1DT12H\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoWeeks()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P2W\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWeeksCannotBeCombinedWithOtherUnits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1Y2W\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNonAscii২ABengali2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P২Y\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestElementWithoutUnit()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllDateAndTimeComponents()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1Y2M3DT4H5M6S\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDateComponentsOnly()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1Y2M3D\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTimeComponentsOnly()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"PT1H2M3S\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMonthAndDay()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1M2D\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHourAndMinute()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"PT1H30M\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMultiDigitValuesInAllComponents()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P10Y10M10DT10H10M10S\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFractionalDurationIsNotAllowedByRfc3339Abnf()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"PT0.5S\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingWhitespaceIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\" P1D\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingWhitespaceIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1D \"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmptyStringIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestYearsAndMonthsCanAppearWithoutDays()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1Y2M\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestYearsAndDaysCannotAppearWithoutMonths()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1Y2D\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMonthsAndDaysCanAppearWithoutYears()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"P1M2D\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHoursAndMinutesCanAppearWithoutSeconds()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"PT1H2M\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHoursAndSecondsCannotAppearWithoutMinutes()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"PT1H2S\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMinutesAndSecondsCanAppearWithoutHour()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"PT1M2S\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/optional/format/duration.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"format\": \"duration\"\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.Format.Duration",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
