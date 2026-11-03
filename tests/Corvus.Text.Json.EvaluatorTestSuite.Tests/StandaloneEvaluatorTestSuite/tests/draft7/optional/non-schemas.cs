using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.NonSchemas;

[TestCategory("Draft7")]
[TestClass]
public class SuiteIdInsideAnEnumIsNotARealIdentifierIdDefinitions
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                        \"type\": \"null\"\n                    }");
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
                "tests/draft7/optional/non-schemas.json",
                "{\n                \"definitions\": {\n                    \"id_in_enum\": {\n                        \"enum\": [\n                            {\n                                \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                                \"type\": \"null\"\n                            }\n                        ]\n                    },\n                    \"real_id_in_schema\": {\n                        \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                        \"type\": \"string\"\n                    },\n                    \"zzz_id_in_const\": {\n                        \"const\": {\n                            \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                            \"type\": \"null\"\n                        }\n                    }\n                },\n                \"anyOf\": [\n                    { \"$ref\": \"#/definitions/id_in_enum\" },\n                    { \"$ref\": \"https://localhost:1234/id/my_identifier.json\" }\n                ]\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

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
                "tests/draft7/optional/non-schemas.json",
                "{\n                \"definitions\": {\n                    \"id_in_unknown0\": {\n                        \"not\": {\n                            \"array_of_schemas\": [\n                                {\n                                    \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\n                                    \"type\": \"null\"\n                                }\n                            ]\n                        }\n                    },\n                    \"real_id_in_schema\": {\n                        \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\n                        \"type\": \"string\"\n                    },\n                    \"id_in_unknown1\": {\n                        \"not\": {\n                            \"object_of_schemas\": {\n                                \"foo\": {\n                                    \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\n                                    \"type\": \"integer\"\n                                }\n                            }\n                        }\n                    }\n                },\n                \"anyOf\": [\n                    { \"$ref\": \"#/definitions/id_in_unknown0\" },\n                    { \"$ref\": \"#/definitions/id_in_unknown1\" },\n                    { \"$ref\": \"https://localhost:1234/unknownKeyword/my_identifier.json\" }\n                ]\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteAnchorInsideAnEnumIsNotARealIdentifierId
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"$id\": \"#my_anchor\",\n                        \"type\": \"null\"\n                    }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestImplementationsThatStripIdMayMatchEitherDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"type\": \"null\"\n                    }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatchRefToId()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a string to match #/definitions/anchor_in_enum\"");
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
                "tests/draft7/optional/non-schemas.json",
                "{\n                \"definitions\": {\n                    \"anchor_in_enum\": {\n                        \"enum\": [\n                            {\n                                \"$id\": \"#my_anchor\",\n                                \"type\": \"null\"\n                            }\n                        ]\n                    },\n                    \"real_identifier_in_schema\": {\n                        \"$id\": \"#my_anchor\",\n                        \"type\": \"string\"\n                    },\n                    \"zzz_anchor_in_const\": {\n                        \"const\": {\n                            \"$id\": \"#my_anchor\",\n                            \"type\": \"null\"\n                        }\n                    }\n                },\n                \"anyOf\": [\n                    { \"$ref\": \"#/definitions/anchor_in_enum\" },\n                    { \"$ref\": \"#my_anchor\" }\n                ]\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
