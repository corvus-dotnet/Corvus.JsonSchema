using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.RecursiveRef;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRecursiveRefWithoutRecursiveAnchorWorksLikeRef
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
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"properties\": {\n                \"foo\": { \"$recursiveRef\": \"#\" }\n            },\n            \"additionalProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRecursiveRefWithoutUsingNesting
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
    public void TestIntegerMatchesAtTheOuterLevel()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLevelMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"hi\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIntegerDoesNotMatchAsAPropertyValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoLevelsPropertiesMatchWithInnerDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": \"hi\" } }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoLevelsNoMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": 1 } }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef2/schema.json\",\n            \"$defs\": {\n                \"myobject\": {\n                    \"$id\": \"myobject.json\",\n                    \"$recursiveAnchor\": true,\n                    \"anyOf\": [\n                        { \"type\": \"string\" },\n                        {\n                            \"type\": \"object\",\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\n                        }\n                    ]\n                }\n            },\n            \"anyOf\": [\n                { \"type\": \"integer\" },\n                { \"$ref\": \"#/$defs/myobject\" }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRecursiveRefWithNesting
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
    public void TestIntegerMatchesAtTheOuterLevel()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLevelMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"hi\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIntegerNowMatchesAsAPropertyValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoLevelsPropertiesMatchWithInnerDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": \"hi\" } }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoLevelsPropertiesMatchWithRecursiveRef()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": 1 } }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef3/schema.json\",\n            \"$recursiveAnchor\": true,\n            \"$defs\": {\n                \"myobject\": {\n                    \"$id\": \"myobject.json\",\n                    \"$recursiveAnchor\": true,\n                    \"anyOf\": [\n                        { \"type\": \"string\" },\n                        {\n                            \"type\": \"object\",\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\n                        }\n                    ]\n                }\n            },\n            \"anyOf\": [\n                { \"type\": \"integer\" },\n                { \"$ref\": \"#/$defs/myobject\" }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRecursiveRefWithRecursiveAnchorFalseWorksLikeRef
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
    public void TestIntegerMatchesAtTheOuterLevel()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLevelMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"hi\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIntegerDoesNotMatchAsAPropertyValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoLevelsPropertiesMatchWithInnerDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": \"hi\" } }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoLevelsIntegerDoesNotMatchAsAPropertyValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": 1 } }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef4/schema.json\",\n            \"$recursiveAnchor\": false,\n            \"$defs\": {\n                \"myobject\": {\n                    \"$id\": \"myobject.json\",\n                    \"$recursiveAnchor\": false,\n                    \"anyOf\": [\n                        { \"type\": \"string\" },\n                        {\n                            \"type\": \"object\",\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\n                        }\n                    ]\n                }\n            },\n            \"anyOf\": [\n                { \"type\": \"integer\" },\n                { \"$ref\": \"#/$defs/myobject\" }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRecursiveRefWithNoRecursiveAnchorWorksLikeRef
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
    public void TestIntegerMatchesAtTheOuterLevel()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleLevelMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"hi\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIntegerDoesNotMatchAsAPropertyValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoLevelsPropertiesMatchWithInnerDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": \"hi\" } }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTwoLevelsIntegerDoesNotMatchAsAPropertyValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": 1 } }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef5/schema.json\",\n            \"$defs\": {\n                \"myobject\": {\n                    \"$id\": \"myobject.json\",\n                    \"$recursiveAnchor\": false,\n                    \"anyOf\": [\n                        { \"type\": \"string\" },\n                        {\n                            \"type\": \"object\",\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\n                        }\n                    ]\n                }\n            },\n            \"anyOf\": [\n                { \"type\": \"integer\" },\n                { \"$ref\": \"#/$defs/myobject\" }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRecursiveRefWithNoRecursiveAnchorInTheInitialTargetSchemaResource
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
    public void TestLeafNodeDoesNotMatchNoRecursion()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": true }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeafNodeMatchesRecursionUsesTheInnerSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": 1 } }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeafNodeDoesNotMatchRecursionUsesTheInnerSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": true } }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef6/base.json\",\n            \"$recursiveAnchor\": true,\n            \"anyOf\": [\n                { \"type\": \"boolean\" },\n                {\n                    \"type\": \"object\",\n                    \"additionalProperties\": {\n                        \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef6/inner.json\",\n                        \"$comment\": \"there is no $recursiveAnchor: true here, so we do NOT recurse to the base\",\n                        \"anyOf\": [\n                            { \"type\": \"integer\" },\n                            { \"type\": \"object\", \"additionalProperties\": { \"$recursiveRef\": \"#\" } }\n                        ]\n                    }\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRecursiveRefWithNoRecursiveAnchorInTheOuterSchemaResource
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
    public void TestLeafNodeDoesNotMatchNoRecursion()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": true }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeafNodeMatchesRecursionOnlyUsesInnerSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": 1 } }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLeafNodeDoesNotMatchRecursionOnlyUsesInnerSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": { \"bar\": true } }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef7/base.json\",\n            \"anyOf\": [\n                { \"type\": \"boolean\" },\n                {\n                    \"type\": \"object\",\n                    \"additionalProperties\": {\n                        \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef7/inner.json\",\n                        \"$recursiveAnchor\": true,\n                        \"anyOf\": [\n                            { \"type\": \"integer\" },\n                            { \"type\": \"object\", \"additionalProperties\": { \"$recursiveRef\": \"#\" } }\n                        ]\n                    }\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteMultipleDynamicPathsToTheRecursiveRefKeyword
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
    public void TestRecurseToAnyLeafNodeFloatsAreAllowed()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"alpha\": 1.1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestRecurseToIntegerNodeFloatsAreNotAllowed()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"november\": 1.1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"https://example.com/recursiveRef8_main.json\",\n            \"$defs\": {\n                \"inner\": {\n                    \"$id\": \"recursiveRef8_inner.json\",\n                    \"$recursiveAnchor\": true,\n                    \"title\": \"inner\",\n                    \"additionalProperties\": {\n                        \"$recursiveRef\": \"#\"\n                    }\n                }\n            },\n            \"if\": {\n                \"propertyNames\": {\n                    \"pattern\": \"^[a-m]\"\n                }\n            },\n            \"then\": {\n                \"title\": \"any type of node\",\n                \"$id\": \"recursiveRef8_anyLeafNode.json\",\n                \"$recursiveAnchor\": true,\n                \"$ref\": \"recursiveRef8_inner.json\"\n            },\n            \"else\": {\n                \"title\": \"integer node\",\n                \"$id\": \"recursiveRef8_integerNode.json\",\n                \"$recursiveAnchor\": true,\n                \"type\": [ \"object\", \"integer\" ],\n                \"$ref\": \"recursiveRef8_inner.json\"\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteDynamicRecursiveRefDestinationNotPredictableAtSchemaCompileTime
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
    public void TestNumericNode()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"alpha\": 1.1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIntegerNode()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"november\": 1.1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"https://example.com/main.json\",\n            \"$defs\": {\n                \"inner\": {\n                    \"$id\": \"inner.json\",\n                    \"$recursiveAnchor\": true,\n                    \"title\": \"inner\",\n                    \"additionalProperties\": {\n                        \"$recursiveRef\": \"#\"\n                    }\n                }\n\n            },\n            \"if\": { \"propertyNames\": { \"pattern\": \"^[a-m]\" } },\n            \"then\": {\n                \"title\": \"any type of node\",\n                \"$id\": \"anyLeafNode.json\",\n                \"$recursiveAnchor\": true,\n                \"$ref\": \"main.json#/$defs/inner\"\n            },\n            \"else\": {\n                \"title\": \"integer node\",\n                \"$id\": \"integerNode.json\",\n                \"$recursiveAnchor\": true,\n                \"type\": [ \"object\", \"integer\" ],\n                \"$ref\": \"main.json#/$defs/inner\"\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
