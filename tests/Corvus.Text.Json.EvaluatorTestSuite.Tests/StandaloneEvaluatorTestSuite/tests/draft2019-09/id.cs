using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.Id;

[TestCategory("Draft201909")]
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
                "tests/draft2019-09/id.json",
                "{\n                \"$id\": \"http://localhost:1234/\",\n                \"items\": {\n                    \"$id\": \"baseUriChange/\",\n                    \"items\": { \"$ref\": \"folderInteger.json\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/baseUriChange/folderInteger.json\": {\n                    \"type\": \"integer\"\n                }\n            }");
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteBaseUriChangeChangeFolder
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
                "tests/draft2019-09/id.json",
                "{\n                \"$id\": \"http://localhost:1234/scope_change_defs1.json\",\n                \"type\" : \"object\",\n                \"properties\": {\n                    \"list\": { \"$ref\": \"baseUriChangeFolder/\" }\n                },\n                \"$defs\": {\n                    \"baz\": {\n                        \"$id\": \"baseUriChangeFolder/\",\n                        \"type\": \"array\",\n                        \"items\": { \"$ref\": \"folderInteger.json\" }\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/baseUriChangeFolder/folderInteger.json\": {\n                    \"type\": \"integer\"\n                }\n            }");
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteBaseUriChangeChangeFolderInSubschema
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
                "tests/draft2019-09/id.json",
                "{\n                \"$id\": \"http://localhost:1234/scope_change_defs2.json\",\n                \"type\" : \"object\",\n                \"properties\": {\n                    \"list\": { \"$ref\": \"baseUriChangeFolderInSubschema/#/$defs/bar\" }\n                },\n                \"$defs\": {\n                    \"baz\": {\n                        \"$id\": \"baseUriChangeFolderInSubschema/\",\n                        \"$defs\": {\n                            \"bar\": {\n                                \"type\": \"array\",\n                                \"items\": { \"$ref\": \"folderInteger.json\" }\n                            }\n                        }\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/baseUriChangeFolderInSubschema/folderInteger.json\": {\n                    \"type\": \"integer\"\n                }\n            }");
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefsWithRelativeUrisAndDefs
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
                "tests/draft2019-09/id.json",
                "{\n                \"$id\": \"http://example.com/schema-relative-uri-defs1.json\",\n                \"properties\": {\n                    \"foo\": {\n                        \"$id\": \"schema-relative-uri-defs2.json\",\n                        \"$defs\": {\n                            \"inner\": {\n                                \"properties\": {\n                                    \"bar\": { \"type\": \"string\" }\n                                }\n                            }\n                        },\n                        \"$ref\": \"#/$defs/inner\"\n                    }\n                },\n                \"$ref\": \"schema-relative-uri-defs2.json\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRelativeRefsWithAbsoluteUrisAndDefs
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
                "tests/draft2019-09/id.json",
                "{\n                \"$id\": \"http://example.com/schema-refs-absolute-uris-defs1.json\",\n                \"properties\": {\n                    \"foo\": {\n                        \"$id\": \"http://example.com/schema-refs-absolute-uris-defs2.json\",\n                        \"$defs\": {\n                            \"inner\": {\n                                \"properties\": {\n                                    \"bar\": { \"type\": \"string\" }\n                                }\n                            }\n                        },\n                        \"$ref\": \"#/$defs/inner\"\n                    }\n                },\n                \"$ref\": \"schema-refs-absolute-uris-defs2.json\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests/draft2019-09/id.json",
                "{\n                \"$id\": \"http://example.com/a.json\",\n                \"$defs\": {\n                    \"x\": {\n                        \"$id\": \"http://example.com/b/c.json\",\n                        \"not\": {\n                            \"$defs\": {\n                                \"y\": {\n                                    \"$id\": \"d.json\",\n                                    \"type\": \"number\"\n                                }\n                            }\n                        }\n                    }\n                },\n                \"allOf\": [{ \"$ref\": \"http://example.com/b/d.json\" }]\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteOrderOfEvaluationIdAndRef
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
    public void TestDataIsValidAgainstFirstDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("5");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDataIsInvalidAgainstFirstDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("50");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/id.json",
                "{\n                \"$comment\": \"$id must be evaluated before $ref to get the proper $ref destination\",\n                \"$id\": \"https://example.com/ref-and-id1/base.json\",\n                \"$ref\": \"int.json\",\n                \"$defs\": {\n                    \"bigint\": {\n                        \"$comment\": \"canonical uri: https://example.com/ref-and-id1/int.json\",\n                        \"$id\": \"int.json\",\n                        \"maximum\": 10\n                    },\n                    \"smallint\": {\n                        \"$comment\": \"canonical uri: https://example.com/ref-and-id1-int.json\",\n                        \"$id\": \"/ref-and-id1-int.json\",\n                        \"maximum\": 2\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteOrderOfEvaluationIdAndAnchorAndRef
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
    public void TestDataIsValidAgainstFirstDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("5");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDataIsInvalidAgainstFirstDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("50");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/id.json",
                "{\n                \"$comment\": \"$id must be evaluated before $ref to get the proper $ref destination\",\n                \"$id\": \"https://example.com/ref-and-id2/base.json\",\n                \"$ref\": \"#bigint\",\n                \"$defs\": {\n                    \"bigint\": {\n                        \"$comment\": \"canonical uri: https://example.com/ref-and-id2/base.json#/$defs/bigint; another valid uri for this location: https://example.com/ref-and-id2/base.json#bigint\",\n                        \"$anchor\": \"bigint\",\n                        \"maximum\": 10\n                    },\n                    \"smallint\": {\n                        \"$comment\": \"canonical uri: https://example.com/ref-and-id2/#/$defs/smallint; another valid uri for this location: https://example.com/ref-and-id2/#bigint\",\n                        \"$id\": \"/ref-and-id2/\",\n                        \"$anchor\": \"bigint\",\n                        \"maximum\": 2\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteOrderOfEvaluationIdAndRefOnNestedSchema
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
    public void TestDataIsValidAgainstNestedSibling()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("5");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDataIsInvalidAgainstNestedSibling()
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
                "tests/draft2019-09/id.json",
                "{\n                \"$comment\": \"$id must be evaluated before $ref to get the proper $ref destination\",\n\n                \"$id\": \"https://example.com/ref-and-id3/base.json\",\n                \"$ref\": \"nested/foo.json\",\n                \"$defs\": {\n                    \"foo\": {\n                        \"$comment\": \"canonical uri: https://example.com/ref-and-id3/nested/foo.json\",\n                        \"$id\": \"nested/foo.json\",\n                        \"$ref\": \"./bar.json\"\n                    },\n                    \"bar\": {\n                        \"$comment\": \"canonical uri: https://example.com/ref-and-id3/nested/bar.json\",\n                        \"$id\": \"nested/bar.json\",\n                        \"type\": \"number\"\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests/draft2019-09/id.json",
                "{\n                \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n                \"$id\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\",\n                \"minimum\": 30,\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests/draft2019-09/id.json",
                "{\n                \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n                \"$id\": \"tag:foo.example,2025:bar/baz\",\n                \"minimum\": 30,\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"tag:foo.example,2025:bar/baz\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests/draft2019-09/id.json",
                "{\n                \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n                \"$id\": \"foo:bar/baz\",\n                \"minimum\": 30,\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"foo:bar/baz\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteSimpleUrnBaseUriWithJsonPointer
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
                "tests/draft2019-09/id.json",
                "{\n                \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\n                \"$id\": \"urn:uuid:deadbeef-1234-00ff-ff00-4321feebdaed\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#/$defs/bar\" }\n                },\n                \"$defs\": {\n                    \"bar\": { \"type\": \"string\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUrnBaseUriWithNss
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
                "tests/draft2019-09/id.json",
                "{\n                \"$comment\": \"RFC 8141 §2.2\",\n                \"$id\": \"urn:example:1/406/47452/2\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#/$defs/bar\" }\n                },\n                \"$defs\": {\n                    \"bar\": { \"type\": \"string\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUrnBaseUriWithRComponent
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
                "tests/draft2019-09/id.json",
                "{\n                \"$comment\": \"RFC 8141 §2.3.1\",\n                \"$id\": \"urn:example:foo-bar-baz-qux?+CCResolve:cc=uk\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#/$defs/bar\" }\n                },\n                \"$defs\": {\n                    \"bar\": { \"type\": \"string\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUrnBaseUriWithQComponent
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
                "tests/draft2019-09/id.json",
                "{\n                \"$comment\": \"RFC 8141 §2.3.2\",\n                \"$id\": \"urn:example:weather?=op=map&lat=39.56&lon=-104.85&datetime=1969-07-21T02:56:15Z\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"#/$defs/bar\" }\n                },\n                \"$defs\": {\n                    \"bar\": { \"type\": \"string\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUrnBaseUriWithUrnAndJsonPointerRef
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
                "tests/draft2019-09/id.json",
                "{\n                \"$id\": \"urn:uuid:deadbeef-1234-0000-0000-4321feebdaed\",\n                \"properties\": {\n                    \"foo\": { \"$ref\": \"urn:uuid:deadbeef-1234-0000-0000-4321feebdaed#/$defs/bar\" }\n                },\n                \"$defs\": {\n                    \"bar\": { \"type\": \"string\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefWithAbsolutePathReference
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnIntegerIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/id.json",
                "{\n                \"$id\": \"http://example.com/ref/absref.json\",\n                \"$defs\": {\n                    \"a\": {\n                        \"$id\": \"http://example.com/ref/absref/foobar.json\",\n                        \"type\": \"number\"\n                    },\n                    \"b\": {\n                        \"$id\": \"http://example.com/absref/foobar.json\",\n                        \"type\": \"string\"\n                    }\n                },\n                \"$ref\": \"/absref/foobar.json\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests/draft2019-09/id.json",
                "{\n                \"$id\": \"file:///folder/file.json\",\n                \"$defs\": {\n                    \"foo\": { \"type\": \"number\" }\n                },\n                \"$ref\": \"#/$defs/foo\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests/draft2019-09/id.json",
                "{\n                \"$id\": \"file:///c:/folder/file.json\",\n                \"$defs\": {\n                    \"foo\": { \"type\": \"number\" }\n                },\n                \"$ref\": \"#/$defs/foo\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUrnRefWithNestedPointerRef
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"bar\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestANonStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/id.json",
                "{\n                \"$ref\": \"urn:uuid:deadbeef-4321-ffff-ffff-1234feebdaed\",\n                \"$defs\": {\n                    \"foo\": {\n                        \"$id\": \"urn:uuid:deadbeef-4321-ffff-ffff-1234feebdaed\",\n                        \"$defs\": {\n                            \"bar\": { \"type\": \"string\" }\n                        },\n                        \"$ref\": \"#/$defs/bar\"\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests/draft2019-09/id.json",
                "{\n                \"$id\": \"http://localhost:1234/some-id\",\n                \"properties\": {\n                    \"name\": { \"$ref\": \"nested/foo-ref-string.json\" }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/nested/foo-ref-string.json\": {\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"foo\": { \"$ref\": \"string.json\" }\n                    }\n                },\n                \"http://localhost:1234/nested/string.json\": {\n                    \"type\": \"string\"\n                }\n            }");
        }
    }
}

[TestCategory("Draft201909")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/id.json",
                "{ \"$ref\": \"http://localhost:1234/different-id-ref-string.json\" }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/different-id-ref-string.json\": {\n                    \"$id\": \"http://localhost:1234/real-id-ref-string.json\",\n                    \"$defs\": {\n                        \"bar\": { \"type\": \"string\" }\n                    },\n                    \"$ref\": \"#/$defs/bar\"\n                }\n            }");
        }
    }
}

[TestCategory("Draft201909")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/id.json",
                "{ \"$ref\": \"http://localhost:1234/urn-ref-string.json\" }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/urn-ref-string.json\": {\n                    \"$id\": \"urn:uuid:feebdaed-ffff-0000-2020-1200deadbeef\",\n                    \"$defs\": {\n                        \"bar\": { \"type\": \"string\" }\n                    },\n                    \"$ref\": \"#/$defs/bar\"\n                }\n            }");
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRemoteHttpRefWithNestedAbsoluteRef
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/id.json",
                "{ \"$ref\": \"http://localhost:1234/nested-absolute-ref-to-string.json\" }",
                "StandaloneEvaluatorTestSuite.Draft201909.Id",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/nested-absolute-ref-to-string.json\": {\n                    \"$defs\": {\n                        \"bar\": {\n                            \"$id\": \"http://localhost:1234/the-nested-id.json\",\n                            \"type\": \"string\"\n                        }\n                    },\n                    \"$ref\": \"http://localhost:1234/the-nested-id.json\"\n                }\n            }");
        }
    }
}
