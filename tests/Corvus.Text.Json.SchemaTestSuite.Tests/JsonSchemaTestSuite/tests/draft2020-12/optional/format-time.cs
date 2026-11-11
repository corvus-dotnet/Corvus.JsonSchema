using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Optional.FormatTime;

[TestCategory("Draft202012")]
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
    public void TestAValidTimeStringWithLeapSecondZulu()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"23:59:60Z\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidLeapSecondZuluWrongHour()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"22:59:60Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidLeapSecondZuluWrongMinute()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"23:58:60Z\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidLeapSecondZeroTimeOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"23:59:60+00:00\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidLeapSecondZeroTimeOffsetWrongHour()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"22:59:60+00:00\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidLeapSecondZeroTimeOffsetWrongMinute()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"23:58:60+00:00\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidLeapSecondPositiveTimeOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"01:29:60+01:30\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidLeapSecondLargePositiveTimeOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"23:29:60+23:30\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidLeapSecondPositiveTimeOffsetWrongHour()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"23:59:60+01:00\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidLeapSecondPositiveTimeOffsetWrongMinute()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"23:59:60+00:30\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidLeapSecondNegativeTimeOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"15:59:60-08:00\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidLeapSecondLargeNegativeTimeOffset()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"00:29:60-23:30\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidLeapSecondNegativeTimeOffsetWrongHour()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"23:59:60-01:00\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidLeapSecondNegativeTimeOffsetWrongMinute()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"23:59:60-00:30\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/optional/format-time.json",
                "{\n                \"format\": \"time\"\n            }",
                "JsonSchemaTestSuite.Draft202012.Optional.FormatTime",
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
