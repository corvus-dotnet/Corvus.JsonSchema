using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft4.AnchorLegacy;

[TestCategory("Draft4")]
[TestClass]
public class SuiteLocationIndependentIdentifierId
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"a\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/anchor-legacy.json",
                "{\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#foo\" }\n                },\n                \"definitions\": {\n                    \"A\": {\n                        \"id\": \"#foo\",\n                        \"type\": \"integer\"\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.AnchorLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteLocationIndependentIdentifierWithBaseUriChangeInSubschemaId
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"a\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/anchor-legacy.json",
                "{\n                \"id\": \"http://localhost:1234/root\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"http://localhost:1234/nested.json#foo\" }\n                },\n                \"definitions\": {\n                    \"A\": {\n                        \"id\": \"nested.json\",\n                        \"definitions\": {\n                            \"B\": {\n                                \"id\": \"#foo\",\n                                \"type\": \"integer\"\n                            }\n                        }\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.AnchorLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteReferenceAnAnchorWithANonRelativeUriId
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"a\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/anchor-legacy.json",
                "{\n                \"id\": \"https://example.com/schema-with-anchor\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"https://example.com/schema-with-anchor#foo\" }\n                },\n                \"definitions\": {\n                    \"A\": {\n                        \"id\": \"#foo\",\n                        \"type\": \"integer\"\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.AnchorLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteUrnBaseUriWithUrnAndAnchorRefId
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
    public void TestAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"bar\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestANonStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 12 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/anchor-legacy.json",
                "{\n                \"id\": \"urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed#something\" }\n                },\n                \"definitions\": {\n                    \"bar\": {\n                        \"id\": \"#something\",\n                        \"type\": \"string\"\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.AnchorLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteAnchorWithinRemoteRefId
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
    public void TestRemoteAnchorValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRemoteAnchorInvalid()
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
                "tests/draft4/anchor-legacy.json",
                "{ \"$ref\": \"http://localhost:1234/locationIndependentIdentifier.json#foo\" }",
                "StandaloneEvaluatorTestSuite.Draft4.AnchorLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/locationIndependentIdentifier.json\": {\n                    \"definitions\": {\n                        \"refToInteger\": {\n                            \"$ref\": \"#foo\"\n                        },\n                        \"A\": {\n                            \"id\": \"#foo\",\n                            \"type\": \"integer\"\n                        }\n                    }\n                }\n            }");
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteLocationIndependentIdentifierInRemoteRefId
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
    public void TestIntegerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/anchor-legacy.json",
                "{ \"$ref\": \"http://localhost:1234/locationIndependentIdentifier.json#/definitions/refToInteger\" }",
                "StandaloneEvaluatorTestSuite.Draft4.AnchorLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/locationIndependentIdentifier.json\": {\n                    \"definitions\": {\n                        \"refToInteger\": {\n                            \"$ref\": \"#foo\"\n                        },\n                        \"A\": {\n                            \"id\": \"#foo\",\n                            \"type\": \"integer\"\n                        }\n                    }\n                }\n            }");
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRefToRefFindsDetachedAnchorId
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
                "tests/draft4/anchor-legacy.json",
                "{ \"$ref\": \"http://localhost:1234/detached-ref.json#/definitions/foo\" }",
                "StandaloneEvaluatorTestSuite.Draft4.AnchorLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/detached-ref.json\": {\n                    \"definitions\": {\n                        \"foo\": {\n                            \"$ref\": \"#detached\"\n                        },\n                        \"detached\": {\n                            \"id\": \"#detached\",\n                            \"type\": \"integer\"\n                        }\n                    }\n                }\n            }");
        }
    }
}
