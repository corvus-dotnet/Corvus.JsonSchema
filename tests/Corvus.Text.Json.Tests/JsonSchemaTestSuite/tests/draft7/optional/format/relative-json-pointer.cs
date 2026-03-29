using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.RelativeJsonPointer;

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteValidationOfRelativeJsonPointersRjp : IClassFixture<SuiteValidationOfRelativeJsonPointersRjp.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfRelativeJsonPointersRjp(Fixture fixture)
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
    public void TestAValidUpwardsRjp()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidDownwardsRjp()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"0/foo/bar\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidUpAndThenDownRjpWithArrayIndex()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"2/0/baz/1/zip\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidRjpTakingTheMemberOrIndexName()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"0#\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidRjpThatIsAValidJsonPointer()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/foo/bar\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNegativePrefix()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"-1/foo/bar\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExplicitPositivePrefix()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"+1/foo/bar\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIsNotAValidJsonPointer()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"0##\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroCannotBeFollowedByOtherDigitsPlusJsonPointer()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"01/a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroCannotBeFollowedByOtherDigitsPlusOctothorpe()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"01#\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmptyString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMultiDigitIntegerPrefix()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"120/foo/bar\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\optional\\format\\relative-json-pointer.json",
                "{ \"format\": \"relative-json-pointer\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.RelativeJsonPointer",
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
