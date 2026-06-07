using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.Id;

[TestCategory("Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                    \"type\": \"null\"\n                }");
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
                "tests/draft7/optional/id.json",
                "{\n            \"definitions\": {\n                \"id_in_enum\": {\n                    \"enum\": [\n                        {\n                          \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                          \"type\": \"null\"\n                        }\n                    ]\n                },\n                \"real_id_in_schema\": {\n                    \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                    \"type\": \"string\"\n                },\n                \"zzz_id_in_const\": {\n                    \"const\": {\n                        \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                        \"type\": \"null\"\n                    }\n                }\n            },\n            \"anyOf\": [\n                { \"$ref\": \"#/definitions/id_in_enum\" },\n                { \"$ref\": \"https://localhost:1234/id/my_identifier.json\" }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Id",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteNonSchemaObjectContainingAPlainNameIdProperty
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
    public void TestSkipTraversingDefinitionForAValidResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"skip not_a_real_anchor\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestConstAtConstNotAnchorDoesNotMatch()
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
                "tests/draft7/optional/id.json",
                "{\n            \"definitions\": {\n                \"const_not_anchor\": {\n                    \"const\": {\n                        \"$id\": \"#not_a_real_anchor\"\n                    }\n                }\n            },\n            \"if\": {\n                \"const\": \"skip not_a_real_anchor\"\n            },\n            \"then\": true,\n            \"else\" : {\n                \"$ref\": \"#/definitions/const_not_anchor\"\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Id",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteNonSchemaObjectContainingAnIdProperty
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
    public void TestSkipTraversingDefinitionForAValidResult()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"skip not_a_real_id\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestConstAtConstNotIdDoesNotMatch()
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
                "tests/draft7/optional/id.json",
                "{\n            \"definitions\": {\n                \"const_not_id\": {\n                    \"const\": {\n                        \"$id\": \"not_a_real_id\"\n                    }\n                }\n            },\n            \"if\": {\n                \"const\": \"skip not_a_real_id\"\n            },\n            \"then\": true,\n            \"else\" : {\n                \"$ref\": \"#/definitions/const_not_id\"\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Id",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
