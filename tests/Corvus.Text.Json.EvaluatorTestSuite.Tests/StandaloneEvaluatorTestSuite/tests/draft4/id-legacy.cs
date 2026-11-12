using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft4.IdLegacy;

[TestCategory("Draft4")]
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
                "tests/draft4/id-legacy.json",
                "{\n                \"id\": \"http://localhost:1234/\",\n                \"items\": {\n                    \"id\": \"baseUriChange/\",\n                    \"items\": { \"$ref\": \"folderInteger.json\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/baseUriChange/folderInteger.json\": {\n                    \"type\": \"integer\"\n                }\n            }");
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteBaseUriChangeChangeFolderId
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"list\": [1] }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"list\": [\"a\"] }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/id-legacy.json",
                "{\n                \"id\": \"http://localhost:1234/scope_change_defs1.json\",\n                \"type\" : \"object\",\n                \"properties\": {\n                    \"list\": { \"$ref\": \"#/definitions/baz\" }\n                },\n                \"definitions\": {\n                    \"baz\": {\n                        \"id\": \"baseUriChangeFolder/\",\n                        \"type\": \"array\",\n                        \"items\": { \"$ref\": \"folderInteger.json\" }\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/baseUriChangeFolder/folderInteger.json\": {\n                    \"type\": \"integer\"\n                }\n            }");
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteBaseUriChangeChangeFolderInSubschemaId
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"list\": [1] }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"list\": [\"a\"] }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/id-legacy.json",
                "{\n                \"id\": \"http://localhost:1234/scope_change_defs2.json\",\n                \"type\" : \"object\",\n                \"properties\": {\n                    \"list\": { \"$ref\": \"#/definitions/baz/definitions/bar\" }\n                },\n                \"definitions\": {\n                    \"baz\": {\n                        \"id\": \"baseUriChangeFolderInSubschema/\",\n                        \"definitions\": {\n                            \"bar\": {\n                                \"type\": \"array\",\n                                \"items\": { \"$ref\": \"folderInteger.json\" }\n                            }\n                        }\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/baseUriChangeFolderInSubschema/folderInteger.json\": {\n                    \"type\": \"integer\"\n                }\n            }");
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"a\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRefResolvesToDefinitionsBaseFooDataValidates()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/id-legacy.json",
                "{\n                \"id\": \"http://localhost:1234/sibling_id/base/\",\n                \"definitions\": {\n                    \"foo\": {\n                        \"id\": \"http://localhost:1234/sibling_id/foo.json\",\n                        \"type\": \"string\"\n                    },\n                    \"base_foo\": {\n                        \"$comment\": \"this canonical uri is http://localhost:1234/sibling_id/base/foo.json\",\n                        \"id\": \"foo.json\",\n                        \"type\": \"number\"\n                    }\n                },\n                \"properties\": {\n                    \"foo\": {\n                        \"$comment\": \"$ref resolves to http://localhost:1234/sibling_id/base/foo.json, not http://localhost:1234/sibling_id/foo.json\",\n                        \"id\": \"http://localhost:1234/sibling_id/\",\n                        \"$ref\": \"foo.json\"\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRefsWithRelativeUrisAndDefsId
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
    public void TestInvalidOnInnerField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"foo\": { \"bar\": 1 },\n                        \"bar\": \"a\"\n                    }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidOnOuterField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"foo\": { \"bar\": \"a\" },\n                        \"bar\": 1\n                    }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidOnBothFields()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"foo\": { \"bar\": \"a\" },\n                        \"bar\": \"a\"\n                    }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/id-legacy.json",
                "{\n                \"id\": \"http://example.com/schema-relative-uri-defs1.json\",\n                \"properties\": {\n                    \"foo\": {\n                        \"id\": \"schema-relative-uri-defs2.json\",\n                        \"definitions\": {\n                            \"inner\": {\n                                \"properties\": {\n                                    \"bar\": { \"type\": \"string\" }\n                                }\n                            }\n                        },\n                        \"allOf\": [{ \"$ref\": \"#/definitions/inner\" }]\n                    }\n                },\n                \"allOf\": [{ \"$ref\": \"schema-relative-uri-defs2.json\" }]\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRelativeRefsWithAbsoluteUrisAndDefsId
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
    public void TestInvalidOnInnerField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"foo\": { \"bar\": 1 },\n                        \"bar\": \"a\"\n                    }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidOnOuterField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"foo\": { \"bar\": \"a\" },\n                        \"bar\": 1\n                    }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidOnBothFields()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                        \"foo\": { \"bar\": \"a\" },\n                        \"bar\": \"a\"\n                    }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/id-legacy.json",
                "{\n                \"id\": \"http://example.com/schema-refs-absolute-uris-defs1.json\",\n                \"properties\": {\n                    \"foo\": {\n                        \"id\": \"http://example.com/schema-refs-absolute-uris-defs2.json\",\n                        \"definitions\": {\n                            \"inner\": {\n                                \"properties\": {\n                                    \"bar\": { \"type\": \"string\" }\n                                }\n                            }\n                        },\n                        \"allOf\": [{ \"$ref\": \"#/definitions/inner\" }]\n                    }\n                },\n                \"allOf\": [{ \"$ref\": \"schema-refs-absolute-uris-defs2.json\" }]\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonNumberIsInvalid()
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
                "tests/draft4/id-legacy.json",
                "{\n                \"id\": \"http://example.com/a.json\",\n                \"definitions\": {\n                    \"x\": {\n                        \"id\": \"http://example.com/b/c.json\",\n                        \"items\": {\n                            \"definitions\": {\n                                \"y\": {\n                                    \"id\": \"d.json\",\n                                    \"type\": \"number\"\n                                }\n                            }\n                        }\n                    }\n                },\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"http://example.com/b/d.json\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteSimpleUrnBaseUriWithRefViaTheUrnId
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
                "tests/draft4/id-legacy.json",
                "{\n                \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n                \"id\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\",\n                \"minimum\": 30,\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteSimpleTagUriBaseUriWithRefViaTheTagUriId
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
                "tests/draft4/id-legacy.json",
                "{\n                \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n                \"id\": \"tag:foo.example,2025:bar/baz\",\n                \"minimum\": 30,\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"tag:foo.example,2025:bar/baz\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteSimpleBaseUriUsingAnArbitraryUriSchemeWithRefToThatUriId
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
                "tests/draft4/id-legacy.json",
                "{\n                \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n                \"id\": \"foo:bar/baz\",\n                \"minimum\": 30,\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"foo:bar/baz\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteSimpleUrnBaseUriWithJsonPointerId
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
                "tests/draft4/id-legacy.json",
                "{\n                \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n                \"id\": \"urn:uuid:deadbeef-1234-00ff-ff00-4321feebdaed\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#/definitions/bar\" }\n                },\n                \"definitions\": {\n                    \"bar\": { \"type\": \"string\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteUrnBaseUriWithNssId
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
                "tests/draft4/id-legacy.json",
                "{\n                \"$comment\": \"RFC 8141 §2.2\",\n                \"id\": \"urn:example:1/406/47452/2\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#/definitions/bar\" }\n                },\n                \"definitions\": {\n                    \"bar\": { \"type\": \"string\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteUrnBaseUriWithRComponentId
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
                "tests/draft4/id-legacy.json",
                "{\n                \"$comment\": \"RFC 8141 §2.3.1\",\n                \"id\": \"urn:example:foo-bar-baz-qux?+CCResolve:cc=uk\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#/definitions/bar\" }\n                },\n                \"definitions\": {\n                    \"bar\": { \"type\": \"string\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteUrnBaseUriWithQComponentId
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
                "tests/draft4/id-legacy.json",
                "{\n                \"$comment\": \"RFC 8141 §2.3.2\",\n                \"id\": \"urn:example:weather?=op=map&lat=39.56&lon=-104.85&datetime=1969-07-21T02:56:15Z\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#/definitions/bar\" }\n                },\n                \"definitions\": {\n                    \"bar\": { \"type\": \"string\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteUrnBaseUriWithUrnAndJsonPointerRefId
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
                "tests/draft4/id-legacy.json",
                "{\n                \"id\": \"urn:uuid:deadbeef-1234-0000-0000-4321feebdaed\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"urn:uuid:deadbeef-1234-0000-0000-4321feebdaed#/definitions/bar\" }\n                },\n                \"definitions\": {\n                    \"bar\": { \"type\": \"string\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRefWithAbsolutePathReferenceId
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"foo\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIntegerIsInvalid()
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
                "tests/draft4/id-legacy.json",
                "{\n                \"id\": \"http://example.com/ref/absref.json\",\n                \"definitions\": {\n                    \"a\": {\n                        \"id\": \"http://example.com/ref/absref/foobar.json\",\n                        \"type\": \"number\"\n                    },\n                    \"b\": {\n                        \"id\": \"http://example.com/absref/foobar.json\",\n                        \"type\": \"string\"\n                    }\n                },\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"/absref/foobar.json\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonNumberIsInvalid()
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
                "tests/draft4/id-legacy.json",
                "{\n                \"id\": \"file:///folder/file.json\",\n                \"definitions\": {\n                    \"foo\": { \"type\": \"number\" }\n                },\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#/definitions/foo\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonNumberIsInvalid()
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
                "tests/draft4/id-legacy.json",
                "{\n                \"id\": \"file:///c:/folder/file.json\",\n                \"definitions\": {\n                    \"foo\": { \"type\": \"number\" }\n                },\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#/definitions/foo\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteUrnRefWithNestedPointerRefId
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": \"bar\" } }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestANonStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": 12 } }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/id-legacy.json",
                "{\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"urn:uuid:deadbeef-4321-ffff-ffff-1234feebdaed\" }\n                },\n                \"definitions\": {\n                    \"foo\": {\n                        \"id\": \"urn:uuid:deadbeef-4321-ffff-ffff-1234feebdaed\",\n                        \"definitions\": {\n                            \"bar\": { \"type\": \"string\" }\n                        },\n                        \"properties\": {\n                            \"bar\": { \"$ref\": \"#/definitions/bar\" }\n                        }\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRemoteHttpRefWithDifferentId
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": \"foo\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/id-legacy.json",
                "{ \"$ref\": \"http://localhost:1234/different-id-ref-string.json\" }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/different-id-ref-string.json\": {\n                    \"id\": \"http://localhost:1234/real-id-ref-string.json\",\n                    \"definitions\": {\n                        \"bar\": { \"type\": \"string\" }\n                    },\n                    \"properties\": {\n                        \"bar\": { \"$ref\": \"#/definitions/bar\" }\n                    }\n                }\n            }");
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRemoteHttpRefWithDifferentUrnId
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": \"foo\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/id-legacy.json",
                "{ \"$ref\": \"http://localhost:1234/urn-ref-string.json\" }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/urn-ref-string.json\": {\n                    \"id\": \"urn:uuid:feebdaed-ffff-0000-2020-1200deadbeef\",\n                    \"definitions\": {\n                        \"bar\": { \"type\": \"string\" }\n                    },\n                    \"properties\": {\n                        \"bar\": { \"$ref\": \"#/definitions/bar\" }\n                    }\n                }\n            }");
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteRemoteHttpRefWithNestedAbsoluteRefId
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": \"foo\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft4/id-legacy.json",
                "{ \"$ref\": \"http://localhost:1234/nested-absolute-ref-to-string.json\" }",
                "StandaloneEvaluatorTestSuite.Draft4.IdLegacy",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/nested-absolute-ref-to-string.json\": {\n                    \"definitions\": {\n                        \"bar\": {\n                            \"id\": \"http://localhost:1234/the-nested-id.json\",\n                            \"type\": \"string\"\n                        }\n                    },\n                    \"properties\": {\n                        \"bar\": { \"$ref\": \"http://localhost:1234/the-nested-id.json\" }\n                    }\n                }\n            }");
        }
    }
}
