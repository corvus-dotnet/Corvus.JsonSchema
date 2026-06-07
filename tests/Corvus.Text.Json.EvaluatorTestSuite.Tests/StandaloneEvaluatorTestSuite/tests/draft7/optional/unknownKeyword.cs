using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.UnknownKeyword;

[TestCategory("Draft7")]
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
                "tests/draft7/optional/unknownKeyword.json",
                "{\n            \"definitions\": {\n                \"id_in_unknown0\": {\n                    \"not\": {\n                        \"array_of_schemas\": [\n                            {\n                              \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\n                              \"type\": \"null\"\n                            }\n                        ]\n                    }\n                },\n                \"real_id_in_schema\": {\n                    \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\n                    \"type\": \"string\"\n                },\n                \"id_in_unknown1\": {\n                    \"not\": {\n                        \"object_of_schemas\": {\n                            \"foo\": {\n                              \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\n                              \"type\": \"integer\"\n                            }\n                        }\n                    }\n                }\n            },\n            \"anyOf\": [\n                { \"$ref\": \"#/definitions/id_in_unknown0\" },\n                { \"$ref\": \"#/definitions/id_in_unknown1\" },\n                { \"$ref\": \"https://localhost:1234/unknownKeyword/my_identifier.json\" }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.UnknownKeyword",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
