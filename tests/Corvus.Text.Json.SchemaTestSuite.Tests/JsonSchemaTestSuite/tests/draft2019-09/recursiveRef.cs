using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft201909.RecursiveRef;

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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": false}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestRecursiveMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"foo\": false } }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMismatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"bar\": false }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestRecursiveMismatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": false } }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"properties\": {\n                \"foo\": { \"$recursiveRef\": \"#\" }\n            },\n            \"additionalProperties\": false\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLevelMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": \"hi\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIntegerDoesNotMatchAsAPropertyValue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoLevelsPropertiesMatchWithInnerDefinition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": \"hi\" } }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoLevelsNoMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": 1 } }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef2/schema.json\",\n            \"$defs\": {\n                \"myobject\": {\n                    \"$id\": \"myobject.json\",\n                    \"$recursiveAnchor\": true,\n                    \"anyOf\": [\n                        { \"type\": \"string\" },\n                        {\n                            \"type\": \"object\",\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\n                        }\n                    ]\n                }\n            },\n            \"anyOf\": [\n                { \"type\": \"integer\" },\n                { \"$ref\": \"#/$defs/myobject\" }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLevelMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": \"hi\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIntegerNowMatchesAsAPropertyValue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoLevelsPropertiesMatchWithInnerDefinition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": \"hi\" } }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoLevelsPropertiesMatchWithRecursiveRef()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": 1 } }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef3/schema.json\",\n            \"$recursiveAnchor\": true,\n            \"$defs\": {\n                \"myobject\": {\n                    \"$id\": \"myobject.json\",\n                    \"$recursiveAnchor\": true,\n                    \"anyOf\": [\n                        { \"type\": \"string\" },\n                        {\n                            \"type\": \"object\",\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\n                        }\n                    ]\n                }\n            },\n            \"anyOf\": [\n                { \"type\": \"integer\" },\n                { \"$ref\": \"#/$defs/myobject\" }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLevelMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": \"hi\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIntegerDoesNotMatchAsAPropertyValue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoLevelsPropertiesMatchWithInnerDefinition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": \"hi\" } }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoLevelsIntegerDoesNotMatchAsAPropertyValue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": 1 } }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef4/schema.json\",\n            \"$recursiveAnchor\": false,\n            \"$defs\": {\n                \"myobject\": {\n                    \"$id\": \"myobject.json\",\n                    \"$recursiveAnchor\": false,\n                    \"anyOf\": [\n                        { \"type\": \"string\" },\n                        {\n                            \"type\": \"object\",\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\n                        }\n                    ]\n                }\n            },\n            \"anyOf\": [\n                { \"type\": \"integer\" },\n                { \"$ref\": \"#/$defs/myobject\" }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleLevelMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": \"hi\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIntegerDoesNotMatchAsAPropertyValue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoLevelsPropertiesMatchWithInnerDefinition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": \"hi\" } }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTwoLevelsIntegerDoesNotMatchAsAPropertyValue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": 1 } }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef5/schema.json\",\n            \"$defs\": {\n                \"myobject\": {\n                    \"$id\": \"myobject.json\",\n                    \"$recursiveAnchor\": false,\n                    \"anyOf\": [\n                        { \"type\": \"string\" },\n                        {\n                            \"type\": \"object\",\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\n                        }\n                    ]\n                }\n            },\n            \"anyOf\": [\n                { \"type\": \"integer\" },\n                { \"$ref\": \"#/$defs/myobject\" }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": true }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeafNodeMatchesRecursionUsesTheInnerSchema()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": 1 } }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeafNodeDoesNotMatchRecursionUsesTheInnerSchema()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": true } }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef6/base.json\",\n            \"$recursiveAnchor\": true,\n            \"anyOf\": [\n                { \"type\": \"boolean\" },\n                {\n                    \"type\": \"object\",\n                    \"additionalProperties\": {\n                        \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef6/inner.json\",\n                        \"$comment\": \"there is no $recursiveAnchor: true here, so we do NOT recurse to the base\",\n                        \"anyOf\": [\n                            { \"type\": \"integer\" },\n                            { \"type\": \"object\", \"additionalProperties\": { \"$recursiveRef\": \"#\" } }\n                        ]\n                    }\n                }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": true }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeafNodeMatchesRecursionOnlyUsesInnerSchema()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": 1 } }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLeafNodeDoesNotMatchRecursionOnlyUsesInnerSchema()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo\": { \"bar\": true } }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef7/base.json\",\n            \"anyOf\": [\n                { \"type\": \"boolean\" },\n                {\n                    \"type\": \"object\",\n                    \"additionalProperties\": {\n                        \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef7/inner.json\",\n                        \"$recursiveAnchor\": true,\n                        \"anyOf\": [\n                            { \"type\": \"integer\" },\n                            { \"type\": \"object\", \"additionalProperties\": { \"$recursiveRef\": \"#\" } }\n                        ]\n                    }\n                }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"alpha\": 1.1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestRecurseToIntegerNodeFloatsAreNotAllowed()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"november\": 1.1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"https://example.com/recursiveRef8_main.json\",\n            \"$defs\": {\n                \"inner\": {\n                    \"$id\": \"recursiveRef8_inner.json\",\n                    \"$recursiveAnchor\": true,\n                    \"title\": \"inner\",\n                    \"additionalProperties\": {\n                        \"$recursiveRef\": \"#\"\n                    }\n                }\n            },\n            \"if\": {\n                \"propertyNames\": {\n                    \"pattern\": \"^[a-m]\"\n                }\n            },\n            \"then\": {\n                \"title\": \"any type of node\",\n                \"$id\": \"recursiveRef8_anyLeafNode.json\",\n                \"$recursiveAnchor\": true,\n                \"$ref\": \"recursiveRef8_inner.json\"\n            },\n            \"else\": {\n                \"title\": \"integer node\",\n                \"$id\": \"recursiveRef8_integerNode.json\",\n                \"$recursiveAnchor\": true,\n                \"type\": [ \"object\", \"integer\" ],\n                \"$ref\": \"recursiveRef8_inner.json\"\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"alpha\": 1.1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIntegerNode()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"november\": 1.1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/recursiveRef.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"https://example.com/main.json\",\n            \"$defs\": {\n                \"inner\": {\n                    \"$id\": \"inner.json\",\n                    \"$recursiveAnchor\": true,\n                    \"title\": \"inner\",\n                    \"additionalProperties\": {\n                        \"$recursiveRef\": \"#\"\n                    }\n                }\n\n            },\n            \"if\": { \"propertyNames\": { \"pattern\": \"^[a-m]\" } },\n            \"then\": {\n                \"title\": \"any type of node\",\n                \"$id\": \"anyLeafNode.json\",\n                \"$recursiveAnchor\": true,\n                \"$ref\": \"main.json#/$defs/inner\"\n            },\n            \"else\": {\n                \"title\": \"integer node\",\n                \"$id\": \"integerNode.json\",\n                \"$recursiveAnchor\": true,\n                \"type\": [ \"object\", \"integer\" ],\n                \"$ref\": \"main.json#/$defs/inner\"\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.RecursiveRef",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
