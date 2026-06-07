using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.UnevaluatedItems;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteUnevaluatedItemsTrue
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
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"unevaluatedItems\": true\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsFalse
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
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsAsSchema
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
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithValidUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithInvalidUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[42]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"unevaluatedItems\": { \"type\": \"string\" }\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithUniformItems
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
    public void TestUnevaluatedItemsDoesnTApply()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"items\": { \"type\": \"string\" },\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithTuple
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
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"prefixItems\": [\n                { \"type\": \"string\" }\n            ],\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithItemsAndPrefixItems
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
    public void TestUnevaluatedItemsDoesnTApply()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", 42]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"prefixItems\": [\n                { \"type\": \"string\" }\n            ],\n            \"items\": true,\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithItems
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
    public void TestValidUnderItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[5, 6, 7, 8]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidUnderItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"items\": {\"type\": \"number\"},\n            \"unevaluatedItems\": {\"type\": \"string\"}\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithNestedTuple
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
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", 42]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", 42, true]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"prefixItems\": [\n                { \"type\": \"string\" }\n            ],\n            \"allOf\": [\n                {\n                    \"prefixItems\": [\n                        true,\n                        { \"type\": \"number\" }\n                    ]\n                }\n            ],\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithNestedItems
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
    public void TestWithOnlyValidAdditionalItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[true, false]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithNoAdditionalItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"yes\", \"no\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithInvalidAdditionalItem()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"yes\", false]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"unevaluatedItems\": {\"type\": \"boolean\"},\n            \"anyOf\": [\n                { \"items\": {\"type\": \"string\"} },\n                true\n            ]\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithNestedPrefixItemsAndItems
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
    public void TestWithNoAdditionalItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithAdditionalItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", 42, true]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"allOf\": [\n                {\n                    \"prefixItems\": [\n                        { \"type\": \"string\" }\n                    ],\n                    \"items\": true\n                }\n            ],\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithNestedUnevaluatedItems
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
    public void TestWithNoAdditionalItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithAdditionalItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", 42, true]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"allOf\": [\n                {\n                    \"prefixItems\": [\n                        { \"type\": \"string\" }\n                    ]\n                },\n                { \"unevaluatedItems\": true }\n            ],\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithAnyOf
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
    public void TestWhenOneSchemaMatchesAndHasNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenOneSchemaMatchesAndHasUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", 42]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenTwoSchemasMatchAndHasNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenTwoSchemasMatchAndHasUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\", 42]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"prefixItems\": [\n                { \"const\": \"foo\" }\n            ],\n            \"anyOf\": [\n                {\n                    \"prefixItems\": [\n                        true,\n                        { \"const\": \"bar\" }\n                    ]\n                },\n                {\n                    \"prefixItems\": [\n                        true,\n                        true,\n                        { \"const\": \"baz\" }\n                    ]\n                }\n            ],\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithOneOf
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
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", 42]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"prefixItems\": [\n                { \"const\": \"foo\" }\n            ],\n            \"oneOf\": [\n                {\n                    \"prefixItems\": [\n                        true,\n                        { \"const\": \"bar\" }\n                    ]\n                },\n                {\n                    \"prefixItems\": [\n                        true,\n                        { \"const\": \"baz\" }\n                    ]\n                }\n            ],\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithNot
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
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"prefixItems\": [\n                { \"const\": \"foo\" }\n            ],\n            \"not\": {\n                \"not\": {\n                    \"prefixItems\": [\n                        true,\n                        { \"const\": \"bar\" }\n                    ]\n                }\n            },\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithIfThenElse
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
    public void TestWhenIfMatchesAndItHasNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"then\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenIfMatchesAndItHasUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"then\", \"else\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenIfDoesnTMatchAndItHasNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", 42, 42, \"else\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenIfDoesnTMatchAndItHasUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", 42, 42, \"else\", 42]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"prefixItems\": [\n                { \"const\": \"foo\" }\n            ],\n            \"if\": {\n                \"prefixItems\": [\n                    true,\n                    { \"const\": \"bar\" }\n                ]\n            },\n            \"then\": {\n                \"prefixItems\": [\n                    true,\n                    true,\n                    { \"const\": \"then\" }\n                ]\n            },\n            \"else\": {\n                \"prefixItems\": [\n                    true,\n                    true,\n                    true,\n                    { \"const\": \"else\" }\n                ]\n            },\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithBooleanSchemas
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
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"allOf\": [true],\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithRef
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
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$ref\": \"#/$defs/bar\",\n            \"prefixItems\": [\n                { \"type\": \"string\" }\n            ],\n            \"unevaluatedItems\": false,\n            \"$defs\": {\n              \"bar\": {\n                  \"prefixItems\": [\n                      true,\n                      { \"type\": \"string\" }\n                  ]\n              }\n            }\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsBeforeRef
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
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"unevaluatedItems\": false,\n            \"prefixItems\": [\n                { \"type\": \"string\" }\n            ],\n            \"$ref\": \"#/$defs/bar\",\n            \"$defs\": {\n              \"bar\": {\n                  \"prefixItems\": [\n                      true,\n                      { \"type\": \"string\" }\n                  ]\n              }\n            }\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithDynamicRef
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
    public void TestWithNoUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\", \"baz\"]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$id\": \"https://example.com/unevaluated-items-with-dynamic-ref/derived\",\n\n            \"$ref\": \"./baseSchema\",\n\n            \"$defs\": {\n                \"derived\": {\n                    \"$dynamicAnchor\": \"addons\",\n                    \"prefixItems\": [\n                        true,\n                        { \"type\": \"string\" }\n                    ]\n                },\n                \"baseSchema\": {\n                    \"$id\": \"./baseSchema\",\n\n                    \"$comment\": \"unevaluatedItems comes first so it's more likely to catch bugs with implementations that are sensitive to keyword ordering\",\n                    \"unevaluatedItems\": false,\n                    \"type\": \"array\",\n                    \"prefixItems\": [\n                        { \"type\": \"string\" }\n                    ],\n                    \"$dynamicRef\": \"#addons\",\n\n                    \"$defs\": {\n                        \"defaultAddons\": {\n                            \"$comment\": \"Needed to satisfy the bookending requirement\",\n                            \"$dynamicAnchor\": \"addons\"\n                        }\n                    }\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsCanTSeeInsideCousins
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
    public void TestAlwaysFails()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"allOf\": [\n                {\n                    \"prefixItems\": [ true ]\n                },\n                { \"unevaluatedItems\": false }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteItemIsEvaluatedInAnUncleSchemaToUnevaluatedItems
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
    public void TestNoExtraItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"foo\": [\n                        \"test\"\n                    ]\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUncleKeywordEvaluationIsNotSignificant()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"foo\": [\n                        \"test\",\n                        \"test\"\n                    ]\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"properties\": {\n                \"foo\": {\n                    \"prefixItems\": [\n                        { \"type\": \"string\" }\n                    ],\n                    \"unevaluatedItems\": false\n                  }\n            },\n            \"anyOf\": [\n                {\n                    \"properties\": {\n                        \"foo\": {\n                            \"prefixItems\": [\n                                true,\n                                { \"type\": \"string\" }\n                            ]\n                        }\n                    }\n                }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsDependsOnAdjacentContains
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
    public void TestSecondItemIsEvaluatedByContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, \"foo\" ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestContainsFailsSecondItemIsNotEvaluated()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 2 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestContainsPassesSecondItemIsNotEvaluated()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 1, 2, \"foo\" ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"prefixItems\": [true],\n            \"contains\": {\"type\": \"string\"},\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsDependsOnMultipleNestedContains
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
    public void Test5NotEvaluatedPassesUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 2, 3, 4, 5, 6 ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void Test7NotEvaluatedFailsUnevaluatedItems()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ 2, 3, 4, 7, 8 ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"allOf\": [\n                { \"contains\": { \"multipleOf\": 2 } },\n                { \"contains\": { \"multipleOf\": 3 } }\n            ],\n            \"unevaluatedItems\": { \"multipleOf\": 5 }\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsAndContainsInteractToControlItemDependencyRelationship
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
    public void TestEmptyArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOnlyASAreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"a\", \"a\" ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestASAndBSAreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"a\", \"b\", \"a\", \"b\", \"a\" ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestASBSAndCSAreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"c\", \"a\", \"c\", \"c\", \"b\", \"a\" ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOnlyBSAreInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"b\", \"b\" ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOnlyCSAreInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"c\", \"c\" ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOnlyBSAndCSAreInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"c\", \"b\", \"c\", \"b\", \"c\" ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOnlyASAndCSAreInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"c\", \"a\", \"c\", \"a\", \"c\" ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"if\": {\n                \"contains\": {\"const\": \"a\"}\n            },\n            \"then\": {\n                \"if\": {\n                    \"contains\": {\"const\": \"b\"}\n                },\n                \"then\": {\n                    \"if\": {\n                        \"contains\": {\"const\": \"c\"}\n                    }\n                }\n            },\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithMinContains0
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
    public void TestEmptyArrayIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoItemsEvaluatedByContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[0]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSomeButNotAllItemsEvaluatedByContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", 0]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllItemsEvaluatedByContains()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"foo\", \"bar\"]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"contains\": {\"type\": \"string\"},\n            \"minContains\": 0,\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteNonArrayInstancesAreValid
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
    public void TestIgnoresBooleans()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("true");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresIntegers()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("123");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresFloats()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1.0");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresObjects()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresStrings()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresNull()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("null");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsWithNullInstanceElements
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
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"unevaluatedItems\": {\n                \"type\": \"null\"\n            }\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteUnevaluatedItemsCanSeeAnnotationsFromIfWithoutThenAndElse
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
    public void TestValidInCaseIfIsEvaluated()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"a\" ]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidInCaseIfIsEvaluated()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[ \"b\" ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"if\": {\n                \"prefixItems\": [{\"const\": \"a\"}]\n            },\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
public class SuiteEvaluatedItemsCollectionNeedsToConsiderInstanceLocation
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
    public void TestWithAnUnevaluatedItemThatExistsAtAnotherLocation()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\n                    [\"foo\", \"bar\"],\n                    \"bar\"\n                ]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/unevaluatedItems.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"prefixItems\": [\n                {\n                    \"prefixItems\": [\n                        true,\n                        { \"type\": \"string\" }\n                    ]\n                }\n            ],\n            \"unevaluatedItems\": false\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedItems",
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
