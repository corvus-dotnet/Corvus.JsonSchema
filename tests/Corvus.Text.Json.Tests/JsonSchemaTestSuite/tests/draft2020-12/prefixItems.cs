using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.PrefixItems;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteASchemaGivenForPrefixItems
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"0\": \"invalid\",\r\n                    \"1\": \"valid\",\r\n                    \"length\": 2\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\prefixItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                {\"type\": \"integer\"},\r\n                {\"type\": \"string\"}\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.PrefixItems",
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
public class SuitePrefixItemsWithBooleanSchemas
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
                "tests\\draft2020-12\\prefixItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [true, false]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.PrefixItems",
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
public class SuiteAdditionalItemsAreAllowedByDefault
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
    public void TestOnlyTheFirstItemIsValidated()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, \"foo\", false]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\prefixItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [{\"type\": \"integer\"}]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.PrefixItems",
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
public class SuitePrefixItemsWithNullInstanceElements
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
                "tests\\draft2020-12\\prefixItems.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                {\r\n                    \"type\": \"null\"\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.PrefixItems",
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
