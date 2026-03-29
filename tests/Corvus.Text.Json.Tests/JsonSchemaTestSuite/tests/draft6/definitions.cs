using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft6.Definitions;

[Trait("JsonSchemaTestSuite", "Draft6")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"definitions\": {\r\n                        \"foo\": {\"type\": \"integer\"}\r\n                    }\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidDefinitionSchema()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"definitions\": {\r\n                        \"foo\": {\"type\": 1}\r\n                    }\r\n                }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\definitions.json",
                "{\"$ref\": \"http://json-schema.org/draft-06/schema#\"}",
                "JsonSchemaTestSuite.Draft6.Definitions",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
