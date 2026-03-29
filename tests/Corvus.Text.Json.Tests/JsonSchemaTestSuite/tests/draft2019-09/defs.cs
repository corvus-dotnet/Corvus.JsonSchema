using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Defs;

[Trait("JsonSchemaTestSuite", "Draft201909")]
public class SuiteValidateDefinitionAgainstMetaschema : IClassFixture<SuiteValidateDefinitionAgainstMetaschema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidateDefinitionAgainstMetaschema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidDefinitionSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"$defs\": {\"foo\": {\"type\": \"integer\"}}}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidDefinitionSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"$defs\": {\"foo\": {\"type\": 1}}}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\defs.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$ref\": \"https://json-schema.org/draft/2019-09/schema\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Defs",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
