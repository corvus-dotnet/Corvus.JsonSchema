using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Properties;

[TestCategory("Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"bar\": \"baz\"}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOnePropertyInvalidIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"bar\": {}}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBothPropertiesInvalidIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": [], \"bar\": {}}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDoesnTInvalidateOtherProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"quux\": []}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresOtherNonObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/properties.json",
                "{\n            \"properties\": {\n                \"foo\": {\"type\": \"integer\"},\n                \"bar\": {\"type\": \"string\"}\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": [1, 2]}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPropertyInvalidatesProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": [1, 2, 3, 4]}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPatternPropertyInvalidatesProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": []}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPatternPropertyValidatesNonproperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"fxo\": [1, 2]}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPatternPropertyInvalidatesNonproperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"fxo\": []}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAdditionalPropertyIgnoresProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": []}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAdditionalPropertyValidatesOthers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"quux\": 3}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAdditionalPropertyInvalidatesOthers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"quux\": \"foo\"}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/properties.json",
                "{\n            \"properties\": {\n                \"foo\": {\"type\": \"array\", \"maxItems\": 3},\n                \"bar\": {\"type\": \"array\"}\n            },\n            \"patternProperties\": {\"f.o\": {\"minItems\": 2}},\n            \"additionalProperties\": {\"type\": \"integer\"}\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuitePropertiesWithBooleanSchema
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
    public void TestNoPropertyPresentIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOnlyTruePropertyPresentIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOnlyFalsePropertyPresentIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 2}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBothPropertiesPresentIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 1, \"bar\": 2}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/properties.json",
                "{\n            \"properties\": {\n                \"foo\": true,\n                \"bar\": false\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\\nbar\": 1,\n                    \"foo\\\"bar\": 1,\n                    \"foo\\\\bar\": 1,\n                    \"foo\\rbar\": 1,\n                    \"foo\\tbar\": 1,\n                    \"foo\\fbar\": 1\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestObjectWithStringsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\\nbar\": \"1\",\n                    \"foo\\\"bar\": \"1\",\n                    \"foo\\\\bar\": \"1\",\n                    \"foo\\rbar\": \"1\",\n                    \"foo\\tbar\": \"1\",\n                    \"foo\\fbar\": \"1\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/properties.json",
                "{\n            \"properties\": {\n                \"foo\\nbar\": {\"type\": \"number\"},\n                \"foo\\\"bar\": {\"type\": \"number\"},\n                \"foo\\\\bar\": {\"type\": \"number\"},\n                \"foo\\rbar\": {\"type\": \"number\"},\n                \"foo\\tbar\": {\"type\": \"number\"},\n                \"foo\\fbar\": {\"type\": \"number\"}\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": null}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/properties.json",
                "{\n            \"properties\": {\n                \"foo\": {\"type\": \"null\"}\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresOtherNonObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoneOfThePropertiesMentioned()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestProtoNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"__proto__\": \"foo\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestToStringNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"toString\": { \"length\": 37 } }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestConstructorNotValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"constructor\": { \"length\": 37 } }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllPresentAndValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \n                    \"__proto__\": 12,\n                    \"toString\": { \"length\": \"foo\" },\n                    \"constructor\": 37\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/properties.json",
                "{\n            \"properties\": {\n                \"__proto__\": {\"type\": \"number\"},\n                \"toString\": {\n                    \"properties\": { \"length\": { \"type\": \"string\" } }\n                },\n                \"constructor\": {\"type\": \"number\"}\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft7.Properties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
