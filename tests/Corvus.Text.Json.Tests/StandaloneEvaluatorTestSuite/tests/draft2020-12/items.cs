using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.Items;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteASchemaGivenForItems
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
    public void TestValidItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2, 3 ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWrongTypeOfItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, \"x\"]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresNonArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\" : \"bar\"}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestJavaScriptPseudoArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"0\": \"invalid\",\r\n                    \"length\": 1\r\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": {\"type\": \"integer\"}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteItemsWithBooleanSchemaTrue
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
    public void TestAnyArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, \"foo\", true ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEmptyArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": true\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteItemsWithBooleanSchemaFalse
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
    public void TestAnyNonEmptyArrayIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, \"foo\", true ]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEmptyArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteItemsAndSubitems
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
    public void TestValidItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTooManyItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTooManySubItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    [ {\"foo\": null}, {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWrongItem()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    {\"foo\": null},\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWrongSubItem()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    [ {}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFewerItemsIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\r\n                    [ {\"foo\": null} ],\r\n                    [ {\"foo\": null} ]\r\n                ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"item\": {\r\n                    \"type\": \"array\",\r\n                    \"items\": false,\r\n                    \"prefixItems\": [\r\n                        { \"$ref\": \"#/$defs/sub-item\" },\r\n                        { \"$ref\": \"#/$defs/sub-item\" }\r\n                    ]\r\n                },\r\n                \"sub-item\": {\r\n                    \"type\": \"object\",\r\n                    \"required\": [\"foo\"]\r\n                }\r\n            },\r\n            \"type\": \"array\",\r\n            \"items\": false,\r\n            \"prefixItems\": [\r\n                { \"$ref\": \"#/$defs/item\" },\r\n                { \"$ref\": \"#/$defs/item\" },\r\n                { \"$ref\": \"#/$defs/item\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteNestedItems
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
    public void TestValidNestedArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[[[1]], [[2],[3]]], [[[4], [5], [6]]]]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNestedArrayWithInvalidType()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[[[\"1\"]], [[2],[3]]], [[[4], [5], [6]]]]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotDeepEnough()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[[1], [2],[3]], [[4], [5], [6]]]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"array\",\r\n            \"items\": {\r\n                \"type\": \"array\",\r\n                \"items\": {\r\n                    \"type\": \"array\",\r\n                    \"items\": {\r\n                        \"type\": \"array\",\r\n                        \"items\": {\r\n                            \"type\": \"number\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuitePrefixItemsWithNoAdditionalItemsAllowed
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
    public void TestEmptyArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFewerNumberOfItemsPresent1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFewerNumberOfItemsPresent2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2 ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEqualNumberOfItemsPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2, 3 ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAdditionalItemsAreNotPermitted()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, 2, 3, 4 ]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{}, {}, {}],\r\n            \"items\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteItemsDoesNotLookInApplicatorsValidCase
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
    public void TestPrefixItemsInAllOfDoesNotConstrainItemsInvalidCase()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 3, 5 ]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPrefixItemsInAllOfDoesNotConstrainItemsValidCase()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 5, 5 ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                { \"prefixItems\": [ { \"minimum\": 3 } ] }\r\n            ],\r\n            \"items\": { \"minimum\": 5 }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuitePrefixItemsValidationAdjustsTheStartingIndexForItems
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
    public void TestValidItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"x\", 2, 3 ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWrongTypeOfSecondItem()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"x\", \"y\" ]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [ { \"type\": \"string\" } ],\r\n            \"items\": { \"type\": \"integer\" }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteItemsWithHeterogeneousArray
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
    public void TestHeterogeneousInvalidInstance()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"foo\", \"bar\", 37 ]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidInstance()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ null ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{}],\r\n            \"items\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteItemsWithNullInstanceElements
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
    public void TestAllowsNullElements()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ null ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
