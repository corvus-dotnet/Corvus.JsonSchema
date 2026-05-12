using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft6.Optional.UnknownKeyword;

[TestCategory("Draft6")]
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft6\\optional\\unknownKeyword.json",
                "{\r\n            \"definitions\": {\r\n                \"id_in_unknown0\": {\r\n                    \"not\": {\r\n                        \"array_of_schemas\": [\r\n                            {\r\n                              \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\r\n                              \"type\": \"null\"\r\n                            }\r\n                        ]\r\n                    }\r\n                },\r\n                \"real_id_in_schema\": {\r\n                    \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"id_in_unknown1\": {\r\n                    \"not\": {\r\n                        \"object_of_schemas\": {\r\n                            \"foo\": {\r\n                              \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\r\n                              \"type\": \"integer\"\r\n                            }\r\n                        }\r\n                    }\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"$ref\": \"#/definitions/id_in_unknown0\" },\r\n                { \"$ref\": \"#/definitions/id_in_unknown1\" },\r\n                { \"$ref\": \"https://localhost:1234/unknownKeyword/my_identifier.json\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.UnknownKeyword",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
