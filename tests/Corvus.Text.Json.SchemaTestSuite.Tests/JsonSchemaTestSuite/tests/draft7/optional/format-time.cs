using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft7.Optional.FormatTime;

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
                "tests/draft7/optional/format-time.json",
                "{\n                \"format\": \"time\"\n            }",
                "JsonSchemaTestSuite.Draft7.Optional.FormatTime",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
