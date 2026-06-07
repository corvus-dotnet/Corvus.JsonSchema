using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Anchor;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteAnchorInsideAnEnumIsNotARealIdentifier
{
    private static Fixture? s_fixture;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        s_fixture = null;
    }

    [TestMethod]
    public void TestExactMatchToEnumAndTypeMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"$anchor\": \"my_anchor\",\n                    \"type\": \"null\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInImplementationsThatStripAnchorThisMayMatchEitherDef()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"type\": \"null\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatchRefToAnchor()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a string to match #/$defs/anchor_in_enum\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoMatchOnEnumOrRefToAnchor()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/optional/anchor.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$defs\": {\n                \"anchor_in_enum\": {\n                    \"enum\": [\n                        {\n                            \"$anchor\": \"my_anchor\",\n                            \"type\": \"null\"\n                        }\n                    ]\n                },\n                \"real_identifier_in_schema\": {\n                    \"$anchor\": \"my_anchor\",\n                    \"type\": \"string\"\n                },\n                \"zzz_anchor_in_const\": {\n                    \"const\": {\n                        \"$anchor\": \"my_anchor\",\n                        \"type\": \"null\"\n                    }\n                }\n            },\n            \"anyOf\": [\n                { \"$ref\": \"#/$defs/anchor_in_enum\" },\n                { \"$ref\": \"#my_anchor\" }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Anchor",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
