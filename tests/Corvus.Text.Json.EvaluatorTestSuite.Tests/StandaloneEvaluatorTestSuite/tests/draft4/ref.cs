using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft4.Ref;

[TestCategory("Draft4")]
[TestClass]
public class SuiteRootPointerRef
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": false}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRecursiveMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": {\"foo\": false}}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": false}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRecursiveMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": {\"bar\": false}}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"properties\": {\n                \"foo\": {\"$ref\": \"#\"}\n            },\n            \"additionalProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRelativePointerRefToObject
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 3}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": true}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"properties\": {\n                \"foo\": {\"type\": \"integer\"},\n                \"bar\": {\"$ref\": \"#/properties/foo\"}\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRelativePointerRefToArray
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
    public void TestMatchArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatchArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, \"foo\"]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"items\": [\n                {\"type\": \"integer\"},\n                {\"$ref\": \"#/items/0\"}\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteEscapedPointerRef
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
    public void TestSlashInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"slash\": \"aoeu\"}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTildeInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"tilde\": \"aoeu\"}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPercentInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"percent\": \"aoeu\"}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSlashValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"slash\": 123}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTildeValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"tilde\": 123}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPercentValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"percent\": 123}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"definitions\": {\n                \"tilde~field\": {\"type\": \"integer\"},\n                \"slash/field\": {\"type\": \"integer\"},\n                \"percent%field\": {\"type\": \"integer\"}\n            },\n            \"properties\": {\n                \"tilde\": {\"$ref\": \"#/definitions/tilde~0field\"},\n                \"slash\": {\"$ref\": \"#/definitions/slash~1field\"},\n                \"percent\": {\"$ref\": \"#/definitions/percent%25field\"}\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteNestedRefs
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
    public void TestNestedRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("5");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNestedRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"definitions\": {\n                \"a\": {\"type\": \"integer\"},\n                \"b\": {\"$ref\": \"#/definitions/a\"},\n                \"c\": {\"$ref\": \"#/definitions/b\"}\n            },\n            \"allOf\": [{ \"$ref\": \"#/definitions/c\" }]\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRefOverridesAnySiblingKeywords
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
    public void TestRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": [] }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRefValidMaxItemsIgnored()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": [ 1, 2, 3] }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"string\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"definitions\": {\n                \"reffed\": {\n                    \"type\": \"array\"\n                }\n            },\n            \"properties\": {\n                \"foo\": {\n                    \"$ref\": \"#/definitions/reffed\",\n                    \"maxItems\": 2\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRefPreventsASiblingIdFromChangingTheBaseUri
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
    public void TestRefResolvesToDefinitionsBaseFooDataDoesNotValidate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRefResolvesToDefinitionsBaseFooDataValidates()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"id\": \"http://localhost:1234/sibling_id/base/\",\n            \"definitions\": {\n                \"foo\": {\n                    \"id\": \"http://localhost:1234/sibling_id/foo.json\",\n                    \"type\": \"string\"\n                },\n                \"base_foo\": {\n                    \"$comment\": \"this canonical uri is http://localhost:1234/sibling_id/base/foo.json\",\n                    \"id\": \"foo.json\",\n                    \"type\": \"number\"\n                }\n            },\n            \"allOf\": [\n                {\n                    \"$comment\": \"$ref resolves to http://localhost:1234/sibling_id/base/foo.json, not http://localhost:1234/sibling_id/foo.json\",\n                    \"id\": \"http://localhost:1234/sibling_id/\",\n                    \"$ref\": \"foo.json\"\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRemoteRefContainingRefsItself
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
    public void TestRemoteRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"minLength\": 1}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRemoteRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"minLength\": -1}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\"$ref\": \"http://json-schema.org/draft-04/schema#\"}",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuitePropertyNamedRefThatIsNotAReference
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
    public void TestPropertyNamedRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"$ref\": \"a\"}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPropertyNamedRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"$ref\": 2}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"properties\": {\n                \"$ref\": {\"type\": \"string\"}\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuitePropertyNamedRefContainingAnActualRef
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
    public void TestPropertyNamedRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"$ref\": \"a\"}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPropertyNamedRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"$ref\": 2}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"properties\": {\n                \"$ref\": {\"$ref\": \"#/definitions/is-string\"}\n            },\n            \"definitions\": {\n                \"is-string\": {\n                    \"type\": \"string\"\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRecursiveReferencesBetweenSchemas
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
    public void TestValidTree()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \n                    \"meta\": \"root\",\n                    \"nodes\": [\n                        {\n                            \"value\": 1,\n                            \"subtree\": {\n                                \"meta\": \"child\",\n                                \"nodes\": [\n                                    {\"value\": 1.1},\n                                    {\"value\": 1.2}\n                                ]\n                            }\n                        },\n                        {\n                            \"value\": 2,\n                            \"subtree\": {\n                                \"meta\": \"child\",\n                                \"nodes\": [\n                                    {\"value\": 2.1},\n                                    {\"value\": 2.2}\n                                ]\n                            }\n                        }\n                    ]\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidTree()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \n                    \"meta\": \"root\",\n                    \"nodes\": [\n                        {\n                            \"value\": 1,\n                            \"subtree\": {\n                                \"meta\": \"child\",\n                                \"nodes\": [\n                                    {\"value\": \"string is invalid\"},\n                                    {\"value\": 1.2}\n                                ]\n                            }\n                        },\n                        {\n                            \"value\": 2,\n                            \"subtree\": {\n                                \"meta\": \"child\",\n                                \"nodes\": [\n                                    {\"value\": 2.1},\n                                    {\"value\": 2.2}\n                                ]\n                            }\n                        }\n                    ]\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"id\": \"http://localhost:1234/tree\",\n            \"description\": \"tree of nodes\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"meta\": {\"type\": \"string\"},\n                \"nodes\": {\n                    \"type\": \"array\",\n                    \"items\": {\"$ref\": \"node\"}\n                }\n            },\n            \"required\": [\"meta\", \"nodes\"],\n            \"definitions\": {\n                \"node\": {\n                    \"id\": \"http://localhost:1234/node\",\n                    \"description\": \"node\",\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"value\": {\"type\": \"number\"},\n                        \"subtree\": {\"$ref\": \"tree\"}\n                    },\n                    \"required\": [\"value\"]\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRefsWithQuote
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
    public void TestObjectWithNumbersIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\\\"bar\": 1\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestObjectWithStringsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\\\"bar\": \"1\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"properties\": {\n                \"foo\\\"bar\": {\"$ref\": \"#/definitions/foo%22bar\"}\n            },\n            \"definitions\": {\n                \"foo\\\"bar\": {\"type\": \"number\"}\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteLocationIndependentIdentifier
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"allOf\": [{\n                \"$ref\": \"#foo\"\n            }],\n            \"definitions\": {\n                \"A\": {\n                    \"id\": \"#foo\",\n                    \"type\": \"integer\"\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteLocationIndependentIdentifierWithBaseUriChangeInSubschema
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"id\": \"http://localhost:1234/root\",\n            \"allOf\": [{\n                \"$ref\": \"http://localhost:1234/nested.json#foo\"\n            }],\n            \"definitions\": {\n                \"A\": {\n                    \"id\": \"nested.json\",\n                    \"definitions\": {\n                        \"B\": {\n                            \"id\": \"#foo\",\n                            \"type\": \"integer\"\n                        }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteNaiveReplacementOfRefWithItsDestinationIsNotCorrect
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
    public void TestDoNotEvaluateTheRefInsideTheEnumMatchingAnyString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"this is a string\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatchTheEnumExactly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"$ref\": \"#/definitions/a_string\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"definitions\": {\n                \"a_string\": { \"type\": \"string\" }\n            },\n            \"enum\": [\n                { \"$ref\": \"#/definitions/a_string\" }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteIdMustBeResolvedAgainstNearestParentNotJustImmediateParent
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
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"id\": \"http://example.com/a.json\",\n            \"definitions\": {\n                \"x\": {\n                    \"id\": \"http://example.com/b/c.json\",\n                    \"not\": {\n                        \"definitions\": {\n                            \"y\": {\n                                \"id\": \"d.json\",\n                                \"type\": \"number\"\n                            }\n                        }\n                    }\n                }\n            },\n            \"allOf\": [\n                {\n                    \"$ref\": \"http://example.com/b/d.json\"\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteIdWithFileUriStillResolvesPointersNix
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
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"id\": \"file:///folder/file.json\",\n            \"definitions\": {\n                \"foo\": {\n                    \"type\": \"number\"\n                }\n            },\n            \"allOf\": [\n                {\n                    \"$ref\": \"#/definitions/foo\"\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteIdWithFileUriStillResolvesPointersWindows
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
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"id\": \"file:///c:/folder/file.json\",\n            \"definitions\": {\n                \"foo\": {\n                    \"type\": \"number\"\n                }\n            },\n            \"allOf\": [\n                {\n                    \"$ref\": \"#/definitions/foo\"\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteEmptyTokensInRefJsonPointer
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
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/ref.json",
                "{\n            \"definitions\": {\n                \"\": {\n                    \"definitions\": {\n                        \"\": { \"type\": \"number\" }\n                    }\n                } \n            },\n            \"allOf\": [\n                {\n                    \"$ref\": \"#/definitions//definitions/\"\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
