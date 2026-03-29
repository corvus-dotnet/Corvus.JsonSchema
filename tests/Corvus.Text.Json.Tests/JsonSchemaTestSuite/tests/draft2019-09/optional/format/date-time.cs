using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Optional.Format.DateTime;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteValidationOfDateTimeStrings : IClassFixture<SuiteValidationOfDateTimeStrings.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfDateTimeStrings(Fixture fixture)
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
    public void TestAValidDateTimeString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1963-06-19T08:30:06.283185Z\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateTimeStringWithoutSecondFraction()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1963-06-19T08:30:06Z\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateTimeStringWithPlusOffset()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1937-01-01T12:00:27.87+00:20\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDateTimeStringWithMinusOffset()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1990-12-31T15:59:50.123-08:00\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidDayInDateTimeString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1990-02-31T15:59:59.123-08:00\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidOffsetInDateTimeString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1990-12-31T15:59:59-24:00\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidClosingZAfterTimeZoneOffset()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1963-06-19T08:30:06.28123+01:00Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidDateTimeString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"06/19/1963 08:30:06 PST\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestCaseInsensitiveTAndZ()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1963-06-19t08:30:06.283185z\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOnlyRfc3339NotAllOfIso8601AreValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2013-350T01:01:01\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidNonPaddedMonthDates()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1963-6-19T08:30:06.283185Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidNonPaddedDayDates()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1963-06-1T08:30:06.283185Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidNonAscii৪ABengali4InDatePortion()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1963-06-1৪T00:00:00Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidNonAscii৪ABengali4InTimePortion()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1963-06-11T0৪:00:00Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidExtendedYear()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"+11963-06-19T08:30:06.283185Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\format\\date-time.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"date-time\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.Format.DateTime",
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
