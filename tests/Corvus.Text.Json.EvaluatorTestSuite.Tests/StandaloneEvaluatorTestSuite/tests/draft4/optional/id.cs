using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft4.Optional.Id;

[TestCategory("Draft4")]
[TestClass]
public class SuiteIdInsideAnEnumIsNotARealIdentifier
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"id\": \"https://localhost:1234/my_identifier.json\",\n                    \"type\": \"null\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatchRefToId()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a string to match #/definitions/id_in_enum\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoMatchOnEnumOrRefToId()
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
                "tests/draft4/optional/id.json",
                "{\n            \"definitions\": {\n                \"id_in_enum\": {\n                    \"enum\": [\n                        {\n                          \"id\": \"https://localhost:1234/my_identifier.json\",\n                          \"type\": \"null\"\n                        }\n                    ]\n                },\n                \"real_id_in_schema\": {\n                    \"id\": \"https://localhost:1234/my_identifier.json\",\n                    \"type\": \"string\"\n                },\n                \"zzz_id_in_const\": {\n                    \"const\": {\n                        \"id\": \"https://localhost:1234/my_identifier.json\",\n                        \"type\": \"null\"\n                    }\n                }\n            },\n            \"anyOf\": [\n                { \"$ref\": \"#/definitions/id_in_enum\" },\n                { \"$ref\": \"https://localhost:1234/my_identifier.json\" }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Optional.Id",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
