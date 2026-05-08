using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.Items;

[TestCategory("Draft201909")]
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
                "tests\\draft2019-09\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": {\"type\": \"integer\"}\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteAnArrayOfSchemasForItems
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
    public void TestCorrectTypes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, \"foo\" ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWrongTypes()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ \"foo\", 1 ]");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIncompleteArrayOfItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestArrayWithAdditionalItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, \"foo\", true ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEmptyArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestJavaScriptPseudoArrayIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"0\": \"invalid\",\r\n                    \"1\": \"valid\",\r\n                    \"length\": 2\r\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [\r\n                {\"type\": \"integer\"},\r\n                {\"type\": \"string\"}\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests\\draft2019-09\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": true\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests\\draft2019-09\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteItemsWithBooleanSchemas
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
    public void TestArrayWithOneItemIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1 ]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestArrayWithTwoItemsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[ 1, \"foo\" ]");
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
                "tests\\draft2019-09\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [true, false]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests\\draft2019-09\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$defs\": {\r\n                \"item\": {\r\n                    \"type\": \"array\",\r\n                    \"additionalItems\": false,\r\n                    \"items\": [\r\n                        { \"$ref\": \"#/$defs/sub-item\" },\r\n                        { \"$ref\": \"#/$defs/sub-item\" }\r\n                    ]\r\n                },\r\n                \"sub-item\": {\r\n                    \"type\": \"object\",\r\n                    \"required\": [\"foo\"]\r\n                }\r\n            },\r\n            \"type\": \"array\",\r\n            \"additionalItems\": false,\r\n            \"items\": [\r\n                { \"$ref\": \"#/$defs/item\" },\r\n                { \"$ref\": \"#/$defs/item\" },\r\n                { \"$ref\": \"#/$defs/item\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests\\draft2019-09\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"array\",\r\n            \"items\": {\r\n                \"type\": \"array\",\r\n                \"items\": {\r\n                    \"type\": \"array\",\r\n                    \"items\": {\r\n                        \"type\": \"array\",\r\n                        \"items\": {\r\n                            \"type\": \"number\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteSingleFormItemsWithNullInstanceElements
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
                "tests\\draft2019-09\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteArrayFormItemsWithNullInstanceElements
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
                "tests\\draft2019-09\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"items\": [\r\n                {\r\n                    \"type\": \"null\"\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
