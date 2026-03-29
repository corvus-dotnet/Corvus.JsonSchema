using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.UnknownKeyword;

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteIdInsideAnUnknownKeywordIsNotARealIdentifier : IClassFixture<SuiteIdInsideAnUnknownKeywordIsNotARealIdentifier.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIdInsideAnUnknownKeywordIsNotARealIdentifier(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestTypeMatchesSecondAnyOfWhichHasARealSchemaInIt()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a string\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTypeMatchesNonSchemaInFirstAnyOf()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTypeMatchesNonSchemaInThirdAnyOf()
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
                "tests\\draft7\\optional\\unknownKeyword.json",
                "{\r\n            \"definitions\": {\r\n                \"id_in_unknown0\": {\r\n                    \"not\": {\r\n                        \"array_of_schemas\": [\r\n                            {\r\n                              \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\r\n                              \"type\": \"null\"\r\n                            }\r\n                        ]\r\n                    }\r\n                },\r\n                \"real_id_in_schema\": {\r\n                    \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"id_in_unknown1\": {\r\n                    \"not\": {\r\n                        \"object_of_schemas\": {\r\n                            \"foo\": {\r\n                              \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\r\n                              \"type\": \"integer\"\r\n                            }\r\n                        }\r\n                    }\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"$ref\": \"#/definitions/id_in_unknown0\" },\r\n                { \"$ref\": \"#/definitions/id_in_unknown1\" },\r\n                { \"$ref\": \"https://localhost:1234/unknownKeyword/my_identifier.json\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft7.Optional.UnknownKeyword",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
