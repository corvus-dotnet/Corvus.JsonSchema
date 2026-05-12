using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.Time;

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
    public void TestAValidTimeString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"08:30:06Z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidTimeStringWithExtraLeadingZeros()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"008:030:006Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidTimeStringWithNoLeadingZeroForSingleDigit()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"8:3:6Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHourMinuteSecondMustBeTwoDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"8:0030:6Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidTimeStringWithSecondFraction()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"23:20:50.52Z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidTimeStringWithPreciseSecondFraction()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"08:30:06.283185Z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidTimeStringWithPlusOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"08:30:06+00:20\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidTimeStringWithMinusOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"08:30:06-08:00\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTimeWithUnknownLocalOffsetIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"12:34:56-00:00\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHourMinuteInTimeOffsetMustBeTwoDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"08:30:06-8:000\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidTimeStringWithCaseInsensitiveZ()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"08:30:06z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidHour()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"24:00:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidMinute()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"00:60:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidSecond()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"00:00:61Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidTimeNumoffsetHour()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"01:02:03+24:00\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidTimeNumoffsetMinute()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"01:02:03+00:60\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidTimeStringWithInvalidTimeWithBothZAndNumoffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"01:02:03Z+00:30\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidOffsetIndicator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"08:30:06 PST\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOnlyRfc3339NotAllOfIso8601AreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"01:01:01,1111\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoTimeOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"12:00:00\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoTimeOffsetWithSecondFraction()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"12:00:00.52\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidNonAscii২ABengali2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1২:00:00Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOffsetNotStartingWithPlusOrMinus()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"08:30:06#00:20\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestContainsLetters()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"ab:cd:ef\"");
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
                "tests\\draft7\\optional\\format\\time.json",
                "{ \"format\": \"time\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.Time",
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
