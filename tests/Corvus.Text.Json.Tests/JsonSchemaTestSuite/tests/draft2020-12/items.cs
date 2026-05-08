using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Items;

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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 2, 3 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongTypeOfItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, \"x\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresNonArrays()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\" : \"bar\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestJavaScriptPseudoArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"0\": \"invalid\",\r\n                    \"length\": 1\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": {\"type\": \"integer\"}\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, \"foo\", true ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmptyArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": true\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, \"foo\", true ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmptyArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTooManyItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTooManySubItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\r\n                    [ {\"foo\": null}, {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongItem()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\r\n                    {\"foo\": null},\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongSubItem()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\r\n                    [ {}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ],\r\n                    [ {\"foo\": null}, {\"foo\": null} ]\r\n                ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFewerItemsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\r\n                    [ {\"foo\": null} ],\r\n                    [ {\"foo\": null} ]\r\n                ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"item\": {\r\n                    \"type\": \"array\",\r\n                    \"items\": false,\r\n                    \"prefixItems\": [\r\n                        { \"$ref\": \"#/$defs/sub-item\" },\r\n                        { \"$ref\": \"#/$defs/sub-item\" }\r\n                    ]\r\n                },\r\n                \"sub-item\": {\r\n                    \"type\": \"object\",\r\n                    \"required\": [\"foo\"]\r\n                }\r\n            },\r\n            \"type\": \"array\",\r\n            \"items\": false,\r\n            \"prefixItems\": [\r\n                { \"$ref\": \"#/$defs/item\" },\r\n                { \"$ref\": \"#/$defs/item\" },\r\n                { \"$ref\": \"#/$defs/item\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[[[[1]], [[2],[3]]], [[[4], [5], [6]]]]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNestedArrayWithInvalidType()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[[[[\"1\"]], [[2],[3]]], [[[4], [5], [6]]]]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotDeepEnough()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[[[1], [2],[3]], [[4], [5], [6]]]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"array\",\r\n            \"items\": {\r\n                \"type\": \"array\",\r\n                \"items\": {\r\n                    \"type\": \"array\",\r\n                    \"items\": {\r\n                        \"type\": \"array\",\r\n                        \"items\": {\r\n                            \"type\": \"number\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFewerNumberOfItemsPresent1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFewerNumberOfItemsPresent2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 2 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEqualNumberOfItemsPresent()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 2, 3 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAdditionalItemsAreNotPermitted()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 2, 3, 4 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{}, {}, {}],\r\n            \"items\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 3, 5 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPrefixItemsInAllOfDoesNotConstrainItemsValidCase()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 5, 5 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                { \"prefixItems\": [ { \"minimum\": 3 } ] }\r\n            ],\r\n            \"items\": { \"minimum\": 5 }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"x\", 2, 3 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongTypeOfSecondItem()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"x\", \"y\" ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [ { \"type\": \"string\" } ],\r\n            \"items\": { \"type\": \"integer\" }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"foo\", \"bar\", 37 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidInstance()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ null ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{}],\r\n            \"items\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ null ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\items.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"items\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
