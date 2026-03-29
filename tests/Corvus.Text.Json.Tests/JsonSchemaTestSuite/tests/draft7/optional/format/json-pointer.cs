using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.JsonPointer;

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteValidationOfJsonPointersJsonStringRepresentation : IClassFixture<SuiteValidationOfJsonPointersJsonStringRepresentation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfJsonPointersJsonStringRepresentation(Fixture fixture)
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
    public void TestAValidJsonPointer()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/foo/bar~0/baz~1/%a\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotAValidJsonPointerNotEscaped()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/foo/bar~\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerWithEmptySegment()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/foo//bar\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerWithTheLastEmptySegment()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/foo/bar/\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69011()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69012()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/foo\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69013()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/foo/0\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69014()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69015()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/a~1b\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69016()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/c%d\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69017()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/e^f\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69018()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/g|h\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69019()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/i\\\\j\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc690110()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/k\\\"l\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc690111()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/ \"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc690112()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/m~0n\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerUsedAddingToTheLastArrayPosition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/foo/-\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerUsedAsObjectMemberName()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/foo/-/bar\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerMultipleEscapedCharacters()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/~1~0~0~1~1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerEscapedWithFractionPart1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/~1.1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidJsonPointerEscapedWithFractionPart2()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/~0.1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotAValidJsonPointerUriFragmentIdentifier1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"#\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotAValidJsonPointerUriFragmentIdentifier2()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"#/\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotAValidJsonPointerUriFragmentIdentifier3()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"#a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotAValidJsonPointerSomeEscapedButNotAll1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/~0~\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotAValidJsonPointerSomeEscapedButNotAll2()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/~0/~\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotAValidJsonPointerWrongEscapeCharacter1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/~2\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotAValidJsonPointerWrongEscapeCharacter2()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/~-1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotAValidJsonPointerMultipleCharactersNotEscaped()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/~~\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotAValidJsonPointerIsnTEmptyNorStartsWith1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotAValidJsonPointerIsnTEmptyNorStartsWith2()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"0\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNotAValidJsonPointerIsnTEmptyNorStartsWith3()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a/a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\optional\\format\\json-pointer.json",
                "{ \"format\": \"json-pointer\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.JsonPointer",
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
