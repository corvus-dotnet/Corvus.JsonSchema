using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.UnknownKeyword;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteIdInsideAnUnknownKeywordIsNotARealIdentifier
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
    public void TestTypeMatchesSecondAnyOfWhichHasARealSchemaInIt()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a string\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTypeMatchesNonSchemaInFirstAnyOf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTypeMatchesNonSchemaInThirdAnyOf()
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
                "tests/draft2020-12/optional/unknownKeyword.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$defs\": {\n                \"id_in_unknown0\": {\n                    \"not\": {\n                        \"array_of_schemas\": [\n                            {\n                              \"$id\": \"https://localhost:1234/draft2020-12/unknownKeyword/my_identifier.json\",\n                              \"type\": \"null\"\n                            }\n                        ]\n                    }\n                },\n                \"real_id_in_schema\": {\n                    \"$id\": \"https://localhost:1234/draft2020-12/unknownKeyword/my_identifier.json\",\n                    \"type\": \"string\"\n                },\n                \"id_in_unknown1\": {\n                    \"not\": {\n                        \"object_of_schemas\": {\n                            \"foo\": {\n                              \"$id\": \"https://localhost:1234/draft2020-12/unknownKeyword/my_identifier.json\",\n                              \"type\": \"integer\"\n                            }\n                        }\n                    }\n                }\n            },\n            \"anyOf\": [\n                { \"$ref\": \"#/$defs/id_in_unknown0\" },\n                { \"$ref\": \"#/$defs/id_in_unknown1\" },\n                { \"$ref\": \"https://localhost:1234/draft2020-12/unknownKeyword/my_identifier.json\" }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.UnknownKeyword",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
