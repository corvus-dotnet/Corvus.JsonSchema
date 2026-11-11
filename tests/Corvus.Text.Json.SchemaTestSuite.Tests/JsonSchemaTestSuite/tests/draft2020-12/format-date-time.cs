using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.FormatDateTime;

[TestCategory("Draft202012")]
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

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/format-date-time.json",
                "{\n                \"format\": \"date-time\"\n            }",
                "JsonSchemaTestSuite.Draft202012.FormatDateTime",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteDateTimeAnnotation
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
    public void TestInvalidDateTimeStringIsOnlyAnAnnotationByDefault()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1990-02-31T15:59:60.123-08:00\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/format-date-time.json",
                "{\n                \"format\": \"date-time\"\n            }",
                "JsonSchemaTestSuite.Draft202012.FormatDateTime",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1963-06-19T08:30:06.283185Z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateTimeStringWithoutSecondFraction()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1963-06-19T08:30:06Z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateTimeStringWithPlusOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1937-01-01T12:00:27.87+00:20\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateTimeStringWithMinusOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1990-12-31T15:59:50.123-08:00\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateTimeWithALeapSecondUtc()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1998-12-31T23:59:60Z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateTimeWithALeapSecondWithMinusOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1998-12-31T15:59:60.123-08:00\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidDateTimePastLeapSecondUtc()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1998-12-31T23:59:61Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidDateTimeWithLeapSecondOnAWrongMinuteUtc()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1998-12-31T23:58:60Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidDateTimeWithLeapSecondOnAWrongHourUtc()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1998-12-31T22:59:60Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidDayInDateTimeString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1990-02-31T15:59:59.123-08:00\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidOffsetInDateTimeString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1990-12-31T15:59:59-24:00\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidClosingZAfterTimeZoneOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1963-06-19T08:30:06.28123+01:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidHourInDateTimeString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1990-12-31T24:00:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidMinuteInDateTimeString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1990-12-31T15:60:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidOffsetMinuteInDateTimeString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1990-12-31T10:00:00+10:60\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidDateTimeString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"06/19/1963 08:30:06 PST\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestCaseInsensitiveTAndZ()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1963-06-19t08:30:06.283185z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOnlyRfc3339NotAllOfIso8601AreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2013-350T01:01:01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNonPaddedMonthDates()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1963-6-19T08:30:06.283185Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNonPaddedDayDates()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1963-06-1T08:30:06.283185Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNonAscii৪ABengali4InDatePortion()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1963-06-1৪T00:00:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNonAscii৪ABengali4InTimePortion()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1963-06-11T0৪:00:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidExtendedYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"+11963-06-19T08:30:06.283185Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestANumericOffsetWithoutMinutesIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1985-04-12T23:20:50+01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHour24IsInvalidEvenWithALeapSecond()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2016-12-31T24:59:60+01:00\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestASecondFractionOfFifteenNinesIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1985-04-12T00:59:59.999999999999999Z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestATrailingNewlineIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1985-04-12T23:20:50Z\\n\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidDateTimeStringWithoutSeconds()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1985-04-12T23:20Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidDateTimeStringWithTrailingContentAfterTheOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1985-04-12T23:20:50Ztail\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidMinuteInDateTimeStringWithANumericOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1985-04-12T23:60:00+00:01\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateTimeStringWith28DaysInFebruaryNormal()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2021-02-28T00:00:00Z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidDateTimeStringWith30DaysInFebruaryLeap()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-02-30T00:00:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void Test2021IsNotALeapYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2021-02-29T00:00:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void Test2020IsALeapYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-02-29T00:00:00Z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestCenturyYear0100IsNotALeapYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0100-02-29T00:00:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestCenturyYear0400IsALeapYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0400-02-29T00:00:00Z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestCenturyYear2100IsNotALeapYear()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2100-02-29T00:00:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestASecondFractionWithNoDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1985-04-12T23:20:50.Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestACommaDecimalSeparatorWithATimeOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1985-04-12T23:20:50,5Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeadingWhitespaceBeforeAValidDateTime()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\" 1985-04-12T23:20:50Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingWhitespaceAfterAValidDateTime()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1985-04-12T23:20:50Z \"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/format-date-time.json",
                "{\n                \"format\": \"date-time\"\n            }",
                "JsonSchemaTestSuite.Draft202012.FormatDateTime",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
