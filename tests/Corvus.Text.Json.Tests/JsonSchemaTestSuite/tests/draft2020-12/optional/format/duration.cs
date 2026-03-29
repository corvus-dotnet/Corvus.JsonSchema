using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Optional.Format.Duration;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteValidationOfDurationStrings : IClassFixture<SuiteValidationOfDurationStrings.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfDurationStrings(Fixture fixture)
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
    public void TestAValidDurationString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P4DT12H30M5S\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidDurationString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"PT1D\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMustStartWithP()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"4DT12H30M5S\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoElementsPresent()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoTimeElementsPresent()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P1YT\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoDateOrTimeElementsPresent()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"PT\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestElementsOutOfOrder()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P2D1Y\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMissingTimeSeparator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P1D2H\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTimeElementInTheDatePosition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P2S\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFourYearsDuration()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P4Y\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroTimeInSeconds()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"PT0S\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroTimeInDays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P0D\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOneMonthDuration()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P1M\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOneMinuteDuration()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"PT1M\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOneAndAHalfDaysInHours()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"PT36H\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestOneAndAHalfDaysInDaysAndHours()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P1DT12H\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTwoWeeks()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P2W\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestWeeksCannotBeCombinedWithOtherUnits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P1Y2W\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidNonAscii২ABengali2()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P২Y\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestElementWithoutUnit()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"P1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format\\duration.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"duration\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.Duration",
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
