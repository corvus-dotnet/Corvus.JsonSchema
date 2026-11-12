using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Id;

[TestCategory("Draft7")]
[TestClass]
public class SuiteBaseUriChange
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
    public void TestBaseUriChangeRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[1]]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBaseUriChangeRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"a\"]]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/id.json",
                "{\n                \"$id\": \"http://localhost:1234/\",\n                \"items\": {\n                    \"$id\": \"baseUriChange/\",\n                    \"items\": { \"$ref\": \"folderInteger.json\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/baseUriChange/folderInteger.json\": {\n                    \"type\": \"integer\"\n                }\n            }");
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteSimpleUrnBaseUriWithRefViaTheUrn
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
    public void TestValidUnderTheUrnIDedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 37 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidUnderTheUrnIDedSchema()
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
                "tests/draft7/id.json",
                "{\n                \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n                \"$id\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\",\n                \"minimum\": 30,\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteSimpleTagUriBaseUriWithRefViaTheTagUri
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
    public void TestValidUnderTheTagUriIDedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 37 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidUnderTheTagUriIDedSchema()
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
                "tests/draft7/id.json",
                "{\n                \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n                \"$id\": \"tag:foo.example,2025:bar/baz\",\n                \"minimum\": 30,\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"tag:foo.example,2025:bar/baz\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteSimpleBaseUriUsingAnArbitraryUriSchemeWithRefToThatUri
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
    public void TestValidUnderTheArbitrarySchemeIDedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 37 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidUnderTheArbitrarySchemeIDedSchema()
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
                "tests/draft7/id.json",
                "{\n                \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n                \"$id\": \"foo:bar/baz\",\n                \"minimum\": 30,\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"foo:bar/baz\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteRetrievedNestedRefsResolveRelativeToTheirUriNotId
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
    public void TestNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"name\": { \"foo\":  1} }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"name\": { \"foo\":  \"a\"} }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/id.json",
                "{\n                \"$id\": \"http://localhost:1234/some-id\",\n                \"properties\": {\n                    \"name\": { \"$ref\": \"nested/foo-ref-string.json\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/nested/foo-ref-string.json\": {\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"foo\": { \"$ref\": \"string.json\" }\n                    }\n                },\n                \"http://localhost:1234/nested/string.json\": {\n                    \"type\": \"string\"\n                }\n            }");
        }
    }
}
