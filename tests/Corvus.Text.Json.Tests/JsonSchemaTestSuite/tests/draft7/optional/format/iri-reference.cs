using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.IriReference;

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteValidationOfIriReferences : IClassFixture<SuiteValidationOfIriReferences.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfIriReferences(Fixture fixture)
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
    public void TestAValidIri()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://ƒøø.ßår/?∂éœ=πîx#πîüx\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidProtocolRelativeIriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"//ƒøø.ßår/?∂éœ=πîx#πîüx\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidRelativeIriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/âππ\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidIriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\\\\\\\WINDOWS\\\\filëßåré\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidIriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"âππ\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidIriFragment()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"#ƒrägmênt\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidIriFragment()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"#ƒräg\\\\mênt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\optional\\format\\iri-reference.json",
                "{ \"format\": \"iri-reference\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.IriReference",
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
