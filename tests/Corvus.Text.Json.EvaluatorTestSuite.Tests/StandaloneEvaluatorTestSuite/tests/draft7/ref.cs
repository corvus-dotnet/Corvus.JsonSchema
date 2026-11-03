using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Ref;

[TestCategory("Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": false }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRecursiveMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"foo\": false } }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": false }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRecursiveMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": false } }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/ref.json",
                "{\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#\" }\n                },\n                \"additionalProperties\": false\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
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
                "tests/draft7/ref.json",
                "{\n                \"properties\": {\n                    \"foo\": { \"type\": \"integer\" },\n                    \"bar\": { \"$ref\": \"#/properties/foo\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"$ref\": \"a\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPropertyNamedRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"$ref\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/ref.json",
                "{\n                \"properties\": {\n                    \"$ref\": { \"type\": \"string\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"meta\": \"root\",\n                        \"nodes\": [\n                            {\n                                \"value\": 1,\n                                \"subtree\": {\n                                    \"meta\": \"child\",\n                                    \"nodes\": [\n                                        { \"value\": 1.1 },\n                                        { \"value\": 1.2 }\n                                    ]\n                                }\n                            },\n                            {\n                                \"value\": 2,\n                                \"subtree\": {\n                                    \"meta\": \"child\",\n                                    \"nodes\": [\n                                        { \"value\": 2.1 },\n                                        { \"value\": 2.2 }\n                                    ]\n                                }\n                            }\n                        ]\n                    }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidTree()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"meta\": \"root\",\n                        \"nodes\": [\n                            {\n                                \"value\": 1,\n                                \"subtree\": {\n                                    \"meta\": \"child\",\n                                    \"nodes\": [\n                                        { \"value\": \"string is invalid\" },\n                                        { \"value\": 1.2 }\n                                    ]\n                                }\n                            },\n                            {\n                                \"value\": 2,\n                                \"subtree\": {\n                                    \"meta\": \"child\",\n                                    \"nodes\": [\n                                        { \"value\": 2.1 },\n                                        { \"value\": 2.2 }\n                                    ]\n                                }\n                            }\n                        ]\n                    }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/ref.json",
                "{ \"$ref\": \"http://localhost:1234/tree\" }",
                "StandaloneEvaluatorTestSuite.Draft7.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/tree\": {\n                    \"description\": \"tree of nodes\",\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"meta\": { \"type\": \"string\" },\n                        \"nodes\": {\n                            \"type\": \"array\",\n                            \"items\": { \"$ref\": \"node\" }\n                        }\n                    }\n                },\n                \"http://localhost:1234/node\": {\n                    \"description\": \"node\",\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"value\": { \"type\": \"number\" },\n                        \"subtree\": { \"$ref\": \"tree\" }\n                    }\n                }\n            }");
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteRefToIf
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
    public void TestANonIntegerIsInvalidDueToTheRef()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIntegerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/ref.json",
                "{\n                \"allOf\": [{ \"$ref\": \"#/if\" }],\n                \"if\": { \"type\": \"integer\" }\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteRefToThen
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
    public void TestANonIntegerIsInvalidDueToTheRef()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIntegerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/ref.json",
                "{\n                \"allOf\": [{ \"$ref\": \"#/then\" }],\n                \"then\": { \"type\": \"integer\" }\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteRefToElse
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
    public void TestANonIntegerIsInvalidDueToTheRef()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIntegerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/ref.json",
                "{\n                \"allOf\": [{ \"$ref\": \"#/else\" }],\n                \"else\": { \"type\": \"integer\" }\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteRemoteRef
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRemoteRefInvalid()
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
                "tests/draft7/ref.json",
                "{ \"$ref\": \"http://localhost:1234/integer.json\" }",
                "StandaloneEvaluatorTestSuite.Draft7.Ref",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/integer.json\": {\n                    \"type\": \"integer\"\n                }\n            }");
        }
    }
}
