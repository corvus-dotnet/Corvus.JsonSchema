using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.Time;

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteValidationOfTimeStrings : IClassFixture<SuiteValidationOfTimeStrings.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfTimeStrings(Fixture fixture)
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
    public void TestAValidTimeString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"08:30:06Z\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidTimeStringWithExtraLeadingZeros()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"008:030:006Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidTimeStringWithNoLeadingZeroForSingleDigit()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"8:3:6Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHourMinuteSecondMustBeTwoDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"8:0030:6Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidTimeStringWithSecondFraction()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"23:20:50.52Z\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidTimeStringWithPreciseSecondFraction()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"08:30:06.283185Z\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidTimeStringWithPlusOffset()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"08:30:06+00:20\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidTimeStringWithMinusOffset()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"08:30:06-08:00\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHourMinuteInTimeOffsetMustBeTwoDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"08:30:06-8:000\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidTimeStringWithCaseInsensitiveZ()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"08:30:06z\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidTimeStringWithInvalidHour()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"24:00:00Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidTimeStringWithInvalidMinute()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"00:60:00Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidTimeStringWithInvalidSecond()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"00:00:61Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidTimeStringWithInvalidTimeNumoffsetHour()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"01:02:03+24:00\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidTimeStringWithInvalidTimeNumoffsetMinute()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"01:02:03+00:60\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidTimeStringWithInvalidTimeWithBothZAndNumoffset()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"01:02:03Z+00:30\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidOffsetIndicator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"08:30:06 PST\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOnlyRfc3339NotAllOfIso8601AreValid()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"01:01:01,1111\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoTimeOffset()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"12:00:00\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoTimeOffsetWithSecondFraction()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"12:00:00.52\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidNonAscii২ABengali2()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1২:00:00Z\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOffsetNotStartingWithPlusOrMinus()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"08:30:06#00:20\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestContainsLetters()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"ab:cd:ef\"");
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
