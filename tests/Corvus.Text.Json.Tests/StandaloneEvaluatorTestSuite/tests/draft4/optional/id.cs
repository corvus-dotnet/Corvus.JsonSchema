using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft4.Optional.Id;

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"id\": \"https://localhost:1234/my_identifier.json\",\r\n                    \"type\": \"null\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatchRefToId()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a string to match #/definitions/id_in_enum\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoMatchOnEnumOrRefToId()
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
                "tests\\draft4\\optional\\id.json",
                "{\r\n            \"definitions\": {\r\n                \"id_in_enum\": {\r\n                    \"enum\": [\r\n                        {\r\n                          \"id\": \"https://localhost:1234/my_identifier.json\",\r\n                          \"type\": \"null\"\r\n                        }\r\n                    ]\r\n                },\r\n                \"real_id_in_schema\": {\r\n                    \"id\": \"https://localhost:1234/my_identifier.json\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"zzz_id_in_const\": {\r\n                    \"const\": {\r\n                        \"id\": \"https://localhost:1234/my_identifier.json\",\r\n                        \"type\": \"null\"\r\n                    }\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"$ref\": \"#/definitions/id_in_enum\" },\r\n                { \"$ref\": \"https://localhost:1234/my_identifier.json\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Optional.Id",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
