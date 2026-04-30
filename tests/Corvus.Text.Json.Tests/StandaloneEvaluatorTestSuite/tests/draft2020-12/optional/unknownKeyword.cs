using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.UnknownKeyword;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a string\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTypeMatchesNonSchemaInFirstAnyOf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTypeMatchesNonSchemaInThirdAnyOf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\unknownKeyword.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"id_in_unknown0\": {\r\n                    \"not\": {\r\n                        \"array_of_schemas\": [\r\n                            {\r\n                              \"$id\": \"https://localhost:1234/draft2020-12/unknownKeyword/my_identifier.json\",\r\n                              \"type\": \"null\"\r\n                            }\r\n                        ]\r\n                    }\r\n                },\r\n                \"real_id_in_schema\": {\r\n                    \"$id\": \"https://localhost:1234/draft2020-12/unknownKeyword/my_identifier.json\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"id_in_unknown1\": {\r\n                    \"not\": {\r\n                        \"object_of_schemas\": {\r\n                            \"foo\": {\r\n                              \"$id\": \"https://localhost:1234/draft2020-12/unknownKeyword/my_identifier.json\",\r\n                              \"type\": \"integer\"\r\n                            }\r\n                        }\r\n                    }\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"$ref\": \"#/$defs/id_in_unknown0\" },\r\n                { \"$ref\": \"#/$defs/id_in_unknown1\" },\r\n                { \"$ref\": \"https://localhost:1234/draft2020-12/unknownKeyword/my_identifier.json\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.UnknownKeyword",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
