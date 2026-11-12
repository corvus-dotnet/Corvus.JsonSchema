using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft6.DependentSchemas;

[TestCategory("Draft6")]
[TestClass]
public class SuiteSingleDependencyDependencies
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
    public void TestValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1, \"bar\": 2 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoDependency()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"quux\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWrongType()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"quux\", \"bar\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWrongTypeOther()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 2, \"bar\": \"quux\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWrongTypeBoth()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"quux\", \"bar\": \"quux\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/dependentSchemas.json",
                "{\n                \"dependencies\": {\n                    \"bar\": {\n                        \"properties\": {\n                            \"foo\": { \"type\": \"integer\" },\n                            \"bar\": { \"type\": \"integer\" }\n                        }\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.DependentSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteBooleanSubschemasDependencies
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
    public void TestObjectWithPropertyHavingSchemaTrueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestObjectWithPropertyHavingSchemaFalseIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestObjectWithBothPropertiesIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1, \"bar\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEmptyObjectIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/dependentSchemas.json",
                "{\n                \"dependencies\": {\n                    \"foo\": true,\n                    \"bar\": false\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.DependentSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteDependentsWithEscapedCharactersDependencies
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
    public void TestValidObject1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\\nbar\": 1, \"foo\\rbar\": 2 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidObject2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\\tbar\": 1, \"a\": 2, \"b\": 3, \"c\": 4 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidObject3()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo'bar\": 1, \"foo\\\"bar\": 2 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidObject1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\\nbar\": 1, \"foo\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidObject2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\\tbar\": 1, \"a\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidObject3()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo'bar\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidObject4()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\\\"bar\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/dependentSchemas.json",
                "{\n                \"dependencies\": {\n                    \"foo\\nbar\": [\"foo\\rbar\"],\n                    \"foo\\tbar\": { \"minProperties\": 4 },\n                    \"foo'bar\": { \"required\": [\"foo\\\"bar\"] },\n                    \"foo\\\"bar\": [\"foo'bar\"]\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.DependentSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteDependentSubschemaIncompatibleWithRootDependencies
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
    public void TestMatchesRoot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatchesDependency()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"bar\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatchesBoth()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": 1, \"bar\": 2 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoDependency()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"baz\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/dependentSchemas.json",
                "{\n                \"properties\": { \"foo\": {} },\n                \"dependencies\": {\n                    \"foo\": {\n                        \"properties\": { \"bar\": {} },\n                        \"additionalProperties\": false\n                    }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft6.DependentSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
