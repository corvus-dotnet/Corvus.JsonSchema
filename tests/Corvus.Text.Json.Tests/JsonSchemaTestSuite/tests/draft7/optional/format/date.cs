using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.Date;

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteValidationOfDateStrings : IClassFixture<SuiteValidationOfDateStrings.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfDateStrings(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllStringFormatsIgnoreIntegers()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreFloats()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("13.7");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreObjects()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreBooleans()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("false");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreNulls()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1963-06-19\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInJanuary()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-01-31\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInJanuary()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-01-32\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith28DaysInFebruaryNormal()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2021-02-28\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith29DaysInFebruaryNormal()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2021-02-29\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith29DaysInFebruaryLeap()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-02-29\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith30DaysInFebruaryLeap()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-02-30\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInMarch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-03-31\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInMarch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-03-32\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith30DaysInApril()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-04-30\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith31DaysInApril()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-04-31\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInMay()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-05-31\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInMay()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-05-32\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith30DaysInJune()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-06-30\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith31DaysInJune()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-06-31\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInJuly()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-07-31\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInJuly()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-07-32\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInAugust()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-08-31\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInAugust()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-08-32\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith30DaysInSeptember()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-09-30\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith31DaysInSeptember()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-09-31\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInOctober()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-10-31\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInOctober()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-10-32\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith30DaysInNovember()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-11-30\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith31DaysInNovember()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-11-31\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateStringWith31DaysInDecember()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-12-31\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWith32DaysInDecember()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-12-32\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAInvalidDateStringWithInvalidMonth()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-13-01\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidDateString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"06/19/1963\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOnlyRfc3339NotAllOfIso8601AreValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2013-350\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonPaddedMonthDatesAreNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1998-1-20\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonPaddedDayDatesAreNotValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1998-01-1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidMonth()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1998-13-01\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidMonthDayCombination()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1998-04-31\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test2021IsNotALeapYear()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2021-02-29\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void Test2020IsALeapYear()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-02-29\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidNonAscii৪ABengali4()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1963-06-1৪\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIso8601NonRfc3339YyyymmddWithoutDashes20230328()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"20230328\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIso8601NonRfc3339WeekNumberImplicitDayOfWeek20230102()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2023-W01\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIso8601NonRfc3339WeekNumberWithDayOfWeek20230328()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2023-W13-2\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIso8601NonRfc3339WeekNumberRolloverToNextYear20230101()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2022W527\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidTimeStringInDateTimeFormat()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2020-11-28T23:55:45Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\optional\\format\\date.json",
                "{ \"format\": \"date\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.Date",
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
