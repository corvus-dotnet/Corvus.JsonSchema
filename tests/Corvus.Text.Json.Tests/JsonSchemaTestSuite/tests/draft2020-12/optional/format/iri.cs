using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Optional.Format.Iri;

[Trait("JsonSchemaTestSuite", "Draft202012")]
public class SuiteValidationOfIrIs : IClassFixture<SuiteValidationOfIrIs.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfIrIs(Fixture fixture)
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
    public void TestAValidIriWithAnchorTag()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://ƒøø.ßår/?∂éœ=πîx#πîüx\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidIriWithAnchorTagAndParentheses()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://ƒøø.com/blah_(wîkïpédiå)_blah#ßité-1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidIriWithUrlEncodedStuff()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://ƒøø.ßår/?q=Test%20URL-encoded%20stuff\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidIriWithManySpecialCharacters()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://-.~_!$&'()*+,;=:%40:80%2f::::::@example.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidIriBasedOnIPv6()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://[2001:0db8:85a3:0000:0000:8a2e:0370:7334]\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidIriBasedOnIPv6()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://2001:0db8:85a3:0000:0000:8a2e:0370:7334\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidRelativeIriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/abc\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidIri()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\\\\\\\WINDOWS\\\\filëßåré\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidIriThoughValidIriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"âππ\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format\\iri.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"iri\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.Iri",
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
