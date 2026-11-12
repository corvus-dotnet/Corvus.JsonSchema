using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.NonSchemas;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteIdInsideAnEnumIsNotARealIdentifierIdDefs
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a string to match #/$defs/id_in_enum\"");
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
                "tests/draft2019-09/optional/non-schemas.json",
                "{\n                \"$defs\": {\n                    \"id_in_enum\": {\n                        \"enum\": [\n                            {\n                                \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                                \"type\": \"null\"\n                            }\n                        ]\n                    },\n                    \"real_id_in_schema\": {\n                        \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                        \"type\": \"string\"\n                    },\n                    \"zzz_id_in_const\": {\n                        \"const\": {\n                            \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                            \"type\": \"null\"\n                        }\n                    }\n                },\n                \"anyOf\": [\n                    { \"$ref\": \"#/$defs/id_in_enum\" },\n                    { \"$ref\": \"https://localhost:1234/id/my_identifier.json\" }\n                ]\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteIdInsideAnUnknownKeywordIsNotARealIdentifierDefs
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
                "tests/draft2019-09/optional/non-schemas.json",
                "{\n                \"$defs\": {\n                    \"id_in_unknown0\": {\n                        \"not\": {\n                            \"array_of_schemas\": [\n                                {\n                                    \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\n                                    \"type\": \"null\"\n                                }\n                            ]\n                        }\n                    },\n                    \"real_id_in_schema\": {\n                        \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\n                        \"type\": \"string\"\n                    },\n                    \"id_in_unknown1\": {\n                        \"not\": {\n                            \"object_of_schemas\": {\n                                \"foo\": {\n                                    \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\n                                    \"type\": \"integer\"\n                                }\n                            }\n                        }\n                    }\n                },\n                \"anyOf\": [\n                    { \"$ref\": \"#/$defs/id_in_unknown0\" },\n                    { \"$ref\": \"#/$defs/id_in_unknown1\" },\n                    { \"$ref\": \"https://localhost:1234/unknownKeyword/my_identifier.json\" }\n                ]\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteAnchorInsideAnEnumIsNotARealIdentifierAnchor
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"$anchor\": \"my_anchor\",\n                        \"type\": \"null\"\n                    }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestImplementationsThatStripAnchorMayMatchEitherDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"type\": \"null\"\n                    }");
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
                "tests/draft2019-09/optional/non-schemas.json",
                "{\n                \"$defs\": {\n                    \"anchor_in_enum\": {\n                        \"enum\": [\n                            {\n                                \"$anchor\": \"my_anchor\",\n                                \"type\": \"null\"\n                            }\n                        ]\n                    },\n                    \"real_identifier_in_schema\": {\n                        \"$anchor\": \"my_anchor\",\n                        \"type\": \"string\"\n                    },\n                    \"zzz_anchor_in_const\": {\n                        \"const\": {\n                            \"$anchor\": \"my_anchor\",\n                            \"type\": \"null\"\n                        }\n                    }\n                },\n                \"anyOf\": [\n                    { \"$ref\": \"#/$defs/anchor_in_enum\" },\n                    { \"$ref\": \"#my_anchor\" }\n                ]\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteReferenceOfARootArbitraryKeyword
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
    public void TestMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": 3 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": true }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/optional/non-schemas.json",
                "{\n                \"unknown-keyword\": { \"type\": \"integer\" },\n                \"properties\": {\n                    \"bar\": { \"$ref\": \"#/unknown-keyword\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteReferenceOfARootArbitraryKeywordWithEncodedRef
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
    public void TestMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": 3 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": true }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/optional/non-schemas.json",
                "{\n                \"unknown/keyword\": { \"type\": \"integer\" },\n                \"properties\": {\n                    \"bar\": { \"$ref\": \"#/unknown~1keyword\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteReferenceOfAnArbitraryKeywordOfASubSchema
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
    public void TestMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": 3 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": true }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/optional/non-schemas.json",
                "{\n                \"properties\": {\n                    \"foo\": {\n                        \"unknown-keyword\": { \"type\": \"integer\" }\n                    },\n                    \"bar\": { \"$ref\": \"#/properties/foo/unknown-keyword\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteReferenceInternalsOfKnownNonApplicator
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
    public void TestMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a string\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("42");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/optional/non-schemas.json",
                "{\n                \"examples\": [{ \"type\": \"string\" }],\n                \"$ref\": \"#/examples/0\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteReferenceOfAnArbitraryKeywordOfASubSchemaWithEncodedRef
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
    public void TestMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": 3 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": true }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/optional/non-schemas.json",
                "{\n                \"properties\": {\n                    \"foo\": {\n                        \"unknown/keyword\": { \"type\": \"integer\" }\n                    },\n                    \"bar\": { \"$ref\": \"#/properties/foo/unknown~1keyword\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
