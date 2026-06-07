using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft4.Properties;

[TestCategory("Draft4")]
[TestClass]
public class SuiteObjectPropertiesValidation
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
    public void TestBothPropertiesPresentAndValidIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": \"baz\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOnePropertyInvalidIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": 1, \"bar\": {}}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBothPropertiesInvalidIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": [], \"bar\": {}}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDoesnTInvalidateOtherProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"quux\": []}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresArrays()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresOtherNonObjects()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("12");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft4/properties.json",
                "{\n            \"properties\": {\n                \"foo\": {\"type\": \"integer\"},\n                \"bar\": {\"type\": \"string\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft4.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuitePropertiesPatternPropertiesAdditionalPropertiesInteraction
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
    public void TestPropertyValidatesProperty()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": [1, 2]}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPropertyInvalidatesProperty()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": [1, 2, 3, 4]}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPatternPropertyInvalidatesProperty()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": []}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPatternPropertyValidatesNonproperty()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"fxo\": [1, 2]}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPatternPropertyInvalidatesNonproperty()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"fxo\": []}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAdditionalPropertyIgnoresProperty()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"bar\": []}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAdditionalPropertyValidatesOthers()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"quux\": 3}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAdditionalPropertyInvalidatesOthers()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"quux\": \"foo\"}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft4/properties.json",
                "{\n            \"properties\": {\n                \"foo\": {\"type\": \"array\", \"maxItems\": 3},\n                \"bar\": {\"type\": \"array\"}\n            },\n            \"patternProperties\": {\"f.o\": {\"minItems\": 2}},\n            \"additionalProperties\": {\"type\": \"integer\"}\n        }",
                "JsonSchemaTestSuite.Draft4.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuitePropertiesWithEscapedCharacters
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
    public void TestObjectWithAllNumbersIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"foo\\nbar\": 1,\n                    \"foo\\\"bar\": 1,\n                    \"foo\\\\bar\": 1,\n                    \"foo\\rbar\": 1,\n                    \"foo\\tbar\": 1,\n                    \"foo\\fbar\": 1\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestObjectWithStringsIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"foo\\nbar\": \"1\",\n                    \"foo\\\"bar\": \"1\",\n                    \"foo\\\\bar\": \"1\",\n                    \"foo\\rbar\": \"1\",\n                    \"foo\\tbar\": \"1\",\n                    \"foo\\fbar\": \"1\"\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft4/properties.json",
                "{\n            \"properties\": {\n                \"foo\\nbar\": {\"type\": \"number\"},\n                \"foo\\\"bar\": {\"type\": \"number\"},\n                \"foo\\\\bar\": {\"type\": \"number\"},\n                \"foo\\rbar\": {\"type\": \"number\"},\n                \"foo\\tbar\": {\"type\": \"number\"},\n                \"foo\\fbar\": {\"type\": \"number\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft4.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuitePropertiesWithNullValuedInstanceProperties
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
    public void TestAllowsNullValues()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": null}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft4/properties.json",
                "{\n            \"properties\": {\n                \"foo\": {\"type\": \"null\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft4.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuitePropertiesWhoseNamesAreJavascriptObjectPropertyNames
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
    public void TestIgnoresArrays()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresOtherNonObjects()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("12");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoneOfThePropertiesMentioned()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestProtoNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"__proto__\": \"foo\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestToStringNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"toString\": { \"length\": 37 } }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestConstructorNotValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"constructor\": { \"length\": 37 } }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllPresentAndValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \n                    \"__proto__\": 12,\n                    \"toString\": { \"length\": \"foo\" },\n                    \"constructor\": 37\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft4/properties.json",
                "{\n            \"properties\": {\n                \"__proto__\": {\"type\": \"number\"},\n                \"toString\": {\n                    \"properties\": { \"length\": { \"type\": \"string\" } }\n                },\n                \"constructor\": {\"type\": \"number\"}\n            }\n        }",
                "JsonSchemaTestSuite.Draft4.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
