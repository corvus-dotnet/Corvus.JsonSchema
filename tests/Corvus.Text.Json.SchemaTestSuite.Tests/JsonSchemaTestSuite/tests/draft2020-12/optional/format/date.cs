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
    public void TestAInvalidDateStringWith29DaysInFebruaryNormal()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2021-02-29\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDateStringWith29DaysInFebruaryLeap()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-02-29\"");
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
    public void TestAInvalidDateStringWithInvalidMonth()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2020-13-01\"");
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
    public void TestInvalidMonthDayCombination()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1998-04-31\"");
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

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format\\date.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"date\"\r\n        }",
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
