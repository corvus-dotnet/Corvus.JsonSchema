using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Optional.Id;

[Trait("JsonSchemaTestSuite", "Draft201909")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\r\n                    \"$id\": \"https://localhost:1234/draft2019-09/id/my_identifier.json\",\r\n                    \"type\": \"null\"\r\n                }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatchRefToId()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a string to match #/$defs/id_in_enum\"");
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
                "tests\\draft2019-09\\optional\\id.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$defs\": {\r\n                \"id_in_enum\": {\r\n                    \"enum\": [\r\n                        {\r\n                          \"$id\": \"https://localhost:1234/draft2019-09/id/my_identifier.json\",\r\n                          \"type\": \"null\"\r\n                        }\r\n                    ]\r\n                },\r\n                \"real_id_in_schema\": {\r\n                    \"$id\": \"https://localhost:1234/draft2019-09/id/my_identifier.json\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"zzz_id_in_const\": {\r\n                    \"const\": {\r\n                        \"$id\": \"https://localhost:1234/draft2019-09/id/my_identifier.json\",\r\n                        \"type\": \"null\"\r\n                    }\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"$ref\": \"#/$defs/id_in_enum\" },\r\n                { \"$ref\": \"https://localhost:1234/draft2019-09/id/my_identifier.json\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.Id",
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
