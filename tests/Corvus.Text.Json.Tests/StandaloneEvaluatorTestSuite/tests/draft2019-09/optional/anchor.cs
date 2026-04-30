using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Anchor;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteAnchorInsideAnEnumIsNotARealIdentifier : IClassFixture<SuiteAnchorInsideAnEnumIsNotARealIdentifier.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnchorInsideAnEnumIsNotARealIdentifier(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestExactMatchToEnumAndTypeMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"$anchor\": \"my_anchor\",\r\n                    \"type\": \"null\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInImplementationsThatStripAnchorThisMayMatchEitherDef()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"type\": \"null\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatchRefToAnchor()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a string to match #/$defs/anchor_in_enum\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNoMatchOnEnumOrRefToAnchor()
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
                "tests\\draft2019-09\\optional\\anchor.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$defs\": {\r\n                \"anchor_in_enum\": {\r\n                    \"enum\": [\r\n                        {\r\n                            \"$anchor\": \"my_anchor\",\r\n                            \"type\": \"null\"\r\n                        }\r\n                    ]\r\n                },\r\n                \"real_identifier_in_schema\": {\r\n                    \"$anchor\": \"my_anchor\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"zzz_anchor_in_const\": {\r\n                    \"const\": {\r\n                        \"$anchor\": \"my_anchor\",\r\n                        \"type\": \"null\"\r\n                    }\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"$ref\": \"#/$defs/anchor_in_enum\" },\r\n                { \"$ref\": \"#my_anchor\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Anchor",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
