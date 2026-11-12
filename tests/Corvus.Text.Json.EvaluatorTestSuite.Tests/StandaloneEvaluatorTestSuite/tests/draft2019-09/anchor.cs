using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.Anchor;

[TestCategory("Draft201909")]
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
                "tests/draft2019-09/anchor.json",
                "{\n                \"$ref\": \"#foo\",\n                \"$defs\": {\n                    \"A\": {\n                        \"$anchor\": \"foo\",\n                        \"type\": \"integer\"\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Anchor",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteLocationIndependentIdentifierWithAbsoluteUri
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
                "tests/draft2019-09/anchor.json",
                "{\n                \"$ref\": \"http://localhost:1234/bar#foo\",\n                \"$defs\": {\n                    \"A\": {\n                        \"$id\": \"http://localhost:1234/bar\",\n                        \"$anchor\": \"foo\",\n                        \"type\": \"integer\"\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Anchor",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests/draft2019-09/anchor.json",
                "{\n                \"$id\": \"http://localhost:1234/root\",\n                \"$ref\": \"http://localhost:1234/nested.json#foo\",\n                \"$defs\": {\n                    \"A\": {\n                        \"$id\": \"nested.json\",\n                        \"$defs\": {\n                            \"B\": {\n                                \"$anchor\": \"foo\",\n                                \"type\": \"integer\"\n                            }\n                        }\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Anchor",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteSameAnchorWithDifferentBaseUri
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
    public void TestRefResolvesToDefsAAllOf1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRefDoesNotResolveToDefsAAllOf0()
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
                "tests/draft2019-09/anchor.json",
                "{\n                \"$id\": \"http://localhost:1234/foobar\",\n                \"$defs\": {\n                    \"A\": {\n                        \"$id\": \"child1\",\n                        \"allOf\": [\n                            {\n                                \"$id\": \"child2\",\n                                \"$anchor\": \"my_anchor\",\n                                \"type\": \"number\"\n                            },\n                            {\n                                \"$anchor\": \"my_anchor\",\n                                \"type\": \"string\"\n                            }\n                        ]\n                    }\n                },\n                \"$ref\": \"child1#my_anchor\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Anchor",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteReferenceAnAnchorWithANonRelativeUri
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
                "tests/draft2019-09/anchor.json",
                "{\n                \"$id\": \"https://example.com/schema-with-anchor\",\n                \"allOf\": [{ \"$ref\": \"https://example.com/schema-with-anchor#foo\" }],\n                \"$defs\": {\n                    \"A\": {\n                        \"$anchor\": \"foo\",\n                        \"type\": \"integer\"\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Anchor",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUrnBaseUriWithUrnAndAnchorRef
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
                "tests/draft2019-09/anchor.json",
                "{\n                \"$id\": \"urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed#something\" }\n                },\n                \"$defs\": {\n                    \"bar\": {\n                        \"$anchor\": \"something\",\n                        \"type\": \"string\"\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Anchor",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteAnchorWithinRemoteRef
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
                "tests/draft2019-09/anchor.json",
                "{ \"$ref\": \"http://localhost:1234/locationIndependentIdentifier.json#foo\" }",
                "StandaloneEvaluatorTestSuite.Draft201909.Anchor",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/locationIndependentIdentifier.json\": {\n                    \"$defs\": {\n                        \"refToInteger\": {\n                            \"$ref\": \"#foo\"\n                        },\n                        \"A\": {\n                            \"$anchor\": \"foo\",\n                            \"type\": \"integer\"\n                        }\n                    }\n                }\n            }");
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteLocationIndependentIdentifierInRemoteRef
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
                "tests/draft2019-09/anchor.json",
                "{ \"$ref\": \"http://localhost:1234/locationIndependentIdentifier.json#/$defs/refToInteger\" }",
                "StandaloneEvaluatorTestSuite.Draft201909.Anchor",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/locationIndependentIdentifier.json\": {\n                    \"$defs\": {\n                        \"refToInteger\": {\n                            \"$ref\": \"#foo\"\n                        },\n                        \"A\": {\n                            \"$anchor\": \"foo\",\n                            \"type\": \"integer\"\n                        }\n                    }\n                }\n            }");
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefToRefFindsDetachedAnchor
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
                "tests/draft2019-09/anchor.json",
                "{ \"$ref\": \"http://localhost:1234/detached-ref.json#/$defs/foo\" }",
                "StandaloneEvaluatorTestSuite.Draft201909.Anchor",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/detached-ref.json\": {\n                    \"$defs\": {\n                        \"foo\": {\n                            \"$ref\": \"#detached\"\n                        },\n                        \"detached\": {\n                            \"$anchor\": \"detached\",\n                            \"type\": \"integer\"\n                        }\n                    }\n                }\n            }");
        }
    }
}
