using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.Vocabulary;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteSchemaThatUsesCustomMetaschemaWithWithNoValidationVocabulary
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
    public void TestApplicatorVocabularyStillWorks()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"badProperty\": \"this property should not exist\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoValidationValidNumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"numberProperty\": 20\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoValidationInvalidNumberButItStillValidates()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"numberProperty\": 1\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/vocabulary.json",
                "{\n            \"$id\": \"https://schema/using/no/validation\",\n            \"$schema\": \"http://localhost:1234/draft2020-12/metaschema-no-validation.json\",\n            \"properties\": {\n                \"badProperty\": false,\n                \"numberProperty\": {\n                    \"minimum\": 10\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Vocabulary",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteIgnoreUnrecognizedOptionalVocabulary
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
    public void TestStringValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foobar\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNumberValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("20");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2020-12/vocabulary.json",
                "{\n            \"$schema\": \"http://localhost:1234/draft2020-12/metaschema-optional-vocabulary.json\",\n            \"type\": \"number\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Vocabulary",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
