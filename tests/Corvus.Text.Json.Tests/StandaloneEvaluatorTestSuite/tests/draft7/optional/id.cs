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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestExactMatchToEnumAndTypeMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"$id\": \"https://localhost:1234/id/my_identifier.json\",\r\n                    \"type\": \"null\"\r\n                }");
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
                "tests\\draft7\\optional\\id.json",
                "{\r\n            \"definitions\": {\r\n                \"id_in_enum\": {\r\n                    \"enum\": [\r\n                        {\r\n                          \"$id\": \"https://localhost:1234/id/my_identifier.json\",\r\n                          \"type\": \"null\"\r\n                        }\r\n                    ]\r\n                },\r\n                \"real_id_in_schema\": {\r\n                    \"$id\": \"https://localhost:1234/id/my_identifier.json\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"zzz_id_in_const\": {\r\n                    \"const\": {\r\n                        \"$id\": \"https://localhost:1234/id/my_identifier.json\",\r\n                        \"type\": \"null\"\r\n                    }\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"$ref\": \"#/definitions/id_in_enum\" },\r\n                { \"$ref\": \"https://localhost:1234/id/my_identifier.json\" }\r\n            ]\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft7\\optional\\id.json",
                "{\r\n            \"definitions\": {\r\n                \"const_not_anchor\": {\r\n                    \"const\": {\r\n                        \"$id\": \"#not_a_real_anchor\"\r\n                    }\r\n                }\r\n            },\r\n            \"if\": {\r\n                \"const\": \"skip not_a_real_anchor\"\r\n            },\r\n            \"then\": true,\r\n            \"else\" : {\r\n                \"$ref\": \"#/definitions/const_not_anchor\"\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft7\\optional\\id.json",
                "{\r\n            \"definitions\": {\r\n                \"const_not_id\": {\r\n                    \"const\": {\r\n                        \"$id\": \"not_a_real_id\"\r\n                    }\r\n                }\r\n            },\r\n            \"if\": {\r\n                \"const\": \"skip not_a_real_id\"\r\n            },\r\n            \"then\": true,\r\n            \"else\" : {\r\n                \"$ref\": \"#/definitions/const_not_id\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Id",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
