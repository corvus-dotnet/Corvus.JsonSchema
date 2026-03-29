using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Optional.Format.UriReference;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteValidationOfUriReferences : IClassFixture<SuiteValidationOfUriReferences.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfUriReferences(Fixture fixture)
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
    public void TestAValidUri()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://foo.bar/?baz=qux#quux\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidProtocolRelativeUriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"//foo.bar/?baz=qux#quux\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidRelativeUriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/abc\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidUriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\\\\\\\WINDOWS\\\\fileshare\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidUriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"abc\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidUriFragment()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"#fragment\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidUriFragment()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"#frag\\\\ment\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUnescapedNonUsAsciiCharacters()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/foobar®.txt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidBackslashCharacter()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"https://example.org/foobar\\\\.txt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format\\uri-reference.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"uri-reference\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.UriReference",
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
