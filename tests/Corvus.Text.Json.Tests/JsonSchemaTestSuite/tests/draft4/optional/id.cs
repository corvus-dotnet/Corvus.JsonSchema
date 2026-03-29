using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft4.Optional.Id;

[Trait("JsonSchemaTestSuite", "Draft4")]
public class SuiteIdInsideAnEnumIsNotARealIdentifier : IClassFixture<SuiteIdInsideAnEnumIsNotARealIdentifier.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIdInsideAnEnumIsNotARealIdentifier(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestExactMatchToEnumAndTypeMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"id\": \"https://localhost:1234/my_identifier.json\",\r\n                    \"type\": \"null\"\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchRefToId()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a string to match #/definitions/id_in_enum\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNoMatchOnEnumOrRefToId()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("1");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\id.json",
                "{\r\n            \"definitions\": {\r\n                \"id_in_enum\": {\r\n                    \"enum\": [\r\n                        {\r\n                          \"id\": \"https://localhost:1234/my_identifier.json\",\r\n                          \"type\": \"null\"\r\n                        }\r\n                    ]\r\n                },\r\n                \"real_id_in_schema\": {\r\n                    \"id\": \"https://localhost:1234/my_identifier.json\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"zzz_id_in_const\": {\r\n                    \"const\": {\r\n                        \"id\": \"https://localhost:1234/my_identifier.json\",\r\n                        \"type\": \"null\"\r\n                    }\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"$ref\": \"#/definitions/id_in_enum\" },\r\n                { \"$ref\": \"https://localhost:1234/my_identifier.json\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.Id",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
