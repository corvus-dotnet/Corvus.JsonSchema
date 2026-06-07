using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft6.Items;

[TestCategory("Draft6")]
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"0\": \"invalid\",\n                    \"length\": 1\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft6/items.json",
                "{\n            \"items\": {\"type\": \"integer\"}\n        }",
                "JsonSchemaTestSuite.Draft6.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestCorrectTypes()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, \"foo\" ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongTypes()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"foo\", 1 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIncompleteArrayOfItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestArrayWithAdditionalItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, \"foo\", true ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmptyArray()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestJavaScriptPseudoArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"0\": \"invalid\",\n                    \"1\": \"valid\",\n                    \"length\": 2\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft6/items.json",
                "{\n            \"items\": [\n                {\"type\": \"integer\"},\n                {\"type\": \"string\"}\n            ]\n        }",
                "JsonSchemaTestSuite.Draft6.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
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
                "tests/draft6/items.json",
                "{\"items\": true}",
                "JsonSchemaTestSuite.Draft6.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
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
                "tests/draft6/items.json",
                "{\"items\": false}",
                "JsonSchemaTestSuite.Draft6.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestArrayWithOneItemIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestArrayWithTwoItemsIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, \"foo\" ]");
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
                "tests/draft6/items.json",
                "{\n            \"items\": [true, false]\n        }",
                "JsonSchemaTestSuite.Draft6.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestValidItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\n                    [ {\"foo\": null}, {\"foo\": null} ],\n                    [ {\"foo\": null}, {\"foo\": null} ],\n                    [ {\"foo\": null}, {\"foo\": null} ]\n                ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTooManyItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\n                    [ {\"foo\": null}, {\"foo\": null} ],\n                    [ {\"foo\": null}, {\"foo\": null} ],\n                    [ {\"foo\": null}, {\"foo\": null} ],\n                    [ {\"foo\": null}, {\"foo\": null} ]\n                ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTooManySubItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\n                    [ {\"foo\": null}, {\"foo\": null}, {\"foo\": null} ],\n                    [ {\"foo\": null}, {\"foo\": null} ],\n                    [ {\"foo\": null}, {\"foo\": null} ]\n                ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongItem()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\n                    {\"foo\": null},\n                    [ {\"foo\": null}, {\"foo\": null} ],\n                    [ {\"foo\": null}, {\"foo\": null} ]\n                ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongSubItem()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\n                    [ {}, {\"foo\": null} ],\n                    [ {\"foo\": null}, {\"foo\": null} ],\n                    [ {\"foo\": null}, {\"foo\": null} ]\n                ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFewerItemsIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\n                    [ {\"foo\": null} ],\n                    [ {\"foo\": null} ]\n                ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft6/items.json",
                "{\n            \"definitions\": {\n                \"item\": {\n                    \"type\": \"array\",\n                    \"additionalItems\": false,\n                    \"items\": [\n                        { \"$ref\": \"#/definitions/sub-item\" },\n                        { \"$ref\": \"#/definitions/sub-item\" }\n                    ]\n                },\n                \"sub-item\": {\n                    \"type\": \"object\",\n                    \"required\": [\"foo\"]\n                }\n            },\n            \"type\": \"array\",\n            \"additionalItems\": false,\n            \"items\": [\n                { \"$ref\": \"#/definitions/item\" },\n                { \"$ref\": \"#/definitions/item\" },\n                { \"$ref\": \"#/definitions/item\" }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft6.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
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
                "tests/draft6/items.json",
                "{\n            \"type\": \"array\",\n            \"items\": {\n                \"type\": \"array\",\n                \"items\": {\n                    \"type\": \"array\",\n                    \"items\": {\n                        \"type\": \"array\",\n                        \"items\": {\n                            \"type\": \"number\"\n                        }\n                    }\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft6.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
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
                "tests/draft6/items.json",
                "{\n            \"items\": {\n                \"type\": \"null\"\n            }\n        }",
                "JsonSchemaTestSuite.Draft6.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
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
                "tests/draft6/items.json",
                "{\n            \"items\": [\n                {\n                    \"type\": \"null\"\n                }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft6.Items",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
