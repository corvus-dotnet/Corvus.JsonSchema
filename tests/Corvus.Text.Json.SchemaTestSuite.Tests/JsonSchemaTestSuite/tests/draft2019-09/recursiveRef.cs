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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"$recursiveRef\": \"#\" }\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef2/schema.json\",\r\n            \"$defs\": {\r\n                \"myobject\": {\r\n                    \"$id\": \"myobject.json\",\r\n                    \"$recursiveAnchor\": true,\r\n                    \"anyOf\": [\r\n                        { \"type\": \"string\" },\r\n                        {\r\n                            \"type\": \"object\",\r\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\r\n                        }\r\n                    ]\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"type\": \"integer\" },\r\n                { \"$ref\": \"#/$defs/myobject\" }\r\n            ]\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef3/schema.json\",\r\n            \"$recursiveAnchor\": true,\r\n            \"$defs\": {\r\n                \"myobject\": {\r\n                    \"$id\": \"myobject.json\",\r\n                    \"$recursiveAnchor\": true,\r\n                    \"anyOf\": [\r\n                        { \"type\": \"string\" },\r\n                        {\r\n                            \"type\": \"object\",\r\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\r\n                        }\r\n                    ]\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"type\": \"integer\" },\r\n                { \"$ref\": \"#/$defs/myobject\" }\r\n            ]\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef4/schema.json\",\r\n            \"$recursiveAnchor\": false,\r\n            \"$defs\": {\r\n                \"myobject\": {\r\n                    \"$id\": \"myobject.json\",\r\n                    \"$recursiveAnchor\": false,\r\n                    \"anyOf\": [\r\n                        { \"type\": \"string\" },\r\n                        {\r\n                            \"type\": \"object\",\r\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\r\n                        }\r\n                    ]\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"type\": \"integer\" },\r\n                { \"$ref\": \"#/$defs/myobject\" }\r\n            ]\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef5/schema.json\",\r\n            \"$defs\": {\r\n                \"myobject\": {\r\n                    \"$id\": \"myobject.json\",\r\n                    \"$recursiveAnchor\": false,\r\n                    \"anyOf\": [\r\n                        { \"type\": \"string\" },\r\n                        {\r\n                            \"type\": \"object\",\r\n                            \"additionalProperties\": { \"$recursiveRef\": \"#\" }\r\n                        }\r\n                    ]\r\n                }\r\n            },\r\n            \"anyOf\": [\r\n                { \"type\": \"integer\" },\r\n                { \"$ref\": \"#/$defs/myobject\" }\r\n            ]\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef6/base.json\",\r\n            \"$recursiveAnchor\": true,\r\n            \"anyOf\": [\r\n                { \"type\": \"boolean\" },\r\n                {\r\n                    \"type\": \"object\",\r\n                    \"additionalProperties\": {\r\n                        \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef6/inner.json\",\r\n                        \"$comment\": \"there is no $recursiveAnchor: true here, so we do NOT recurse to the base\",\r\n                        \"anyOf\": [\r\n                            { \"type\": \"integer\" },\r\n                            { \"type\": \"object\", \"additionalProperties\": { \"$recursiveRef\": \"#\" } }\r\n                        ]\r\n                    }\r\n                }\r\n            ]\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef7/base.json\",\r\n            \"anyOf\": [\r\n                { \"type\": \"boolean\" },\r\n                {\r\n                    \"type\": \"object\",\r\n                    \"additionalProperties\": {\r\n                        \"$id\": \"http://localhost:4242/draft2019-09/recursiveRef7/inner.json\",\r\n                        \"$recursiveAnchor\": true,\r\n                        \"anyOf\": [\r\n                            { \"type\": \"integer\" },\r\n                            { \"type\": \"object\", \"additionalProperties\": { \"$recursiveRef\": \"#\" } }\r\n                        ]\r\n                    }\r\n                }\r\n            ]\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"https://example.com/recursiveRef8_main.json\",\r\n            \"$defs\": {\r\n                \"inner\": {\r\n                    \"$id\": \"recursiveRef8_inner.json\",\r\n                    \"$recursiveAnchor\": true,\r\n                    \"title\": \"inner\",\r\n                    \"additionalProperties\": {\r\n                        \"$recursiveRef\": \"#\"\r\n                    }\r\n                }\r\n            },\r\n            \"if\": {\r\n                \"propertyNames\": {\r\n                    \"pattern\": \"^[a-m]\"\r\n                }\r\n            },\r\n            \"then\": {\r\n                \"title\": \"any type of node\",\r\n                \"$id\": \"recursiveRef8_anyLeafNode.json\",\r\n                \"$recursiveAnchor\": true,\r\n                \"$ref\": \"recursiveRef8_inner.json\"\r\n            },\r\n            \"else\": {\r\n                \"title\": \"integer node\",\r\n                \"$id\": \"recursiveRef8_integerNode.json\",\r\n                \"$recursiveAnchor\": true,\r\n                \"type\": [ \"object\", \"integer\" ],\r\n                \"$ref\": \"recursiveRef8_inner.json\"\r\n            }\r\n        }",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft2019-09\\recursiveRef.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"https://example.com/main.json\",\r\n            \"$defs\": {\r\n                \"inner\": {\r\n                    \"$id\": \"inner.json\",\r\n                    \"$recursiveAnchor\": true,\r\n                    \"title\": \"inner\",\r\n                    \"additionalProperties\": {\r\n                        \"$recursiveRef\": \"#\"\r\n                    }\r\n                }\r\n\r\n            },\r\n            \"if\": { \"propertyNames\": { \"pattern\": \"^[a-m]\" } },\r\n            \"then\": {\r\n                \"title\": \"any type of node\",\r\n                \"$id\": \"anyLeafNode.json\",\r\n                \"$recursiveAnchor\": true,\r\n                \"$ref\": \"main.json#/$defs/inner\"\r\n            },\r\n            \"else\": {\r\n                \"title\": \"integer node\",\r\n                \"$id\": \"integerNode.json\",\r\n                \"$recursiveAnchor\": true,\r\n                \"type\": [ \"object\", \"integer\" ],\r\n                \"$ref\": \"main.json#/$defs/inner\"\r\n            }\r\n        }",
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
