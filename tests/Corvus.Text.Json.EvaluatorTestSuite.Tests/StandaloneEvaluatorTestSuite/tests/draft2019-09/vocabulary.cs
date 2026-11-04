using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.Vocabulary;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteSchemaThatUsesCustomMetaschemaWithNoValidationVocabularyRecursiveAnchor
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"badProperty\": \"this property should not exist\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoValidationValidNumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"numberProperty\": 20 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoValidationInvalidNumberButItStillValidates()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"numberProperty\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/vocabulary.json",
                "{\n                \"$schema\": \"http://localhost:1234/draft2019-09/metaschema-no-validation.json\",\n                \"properties\": {\n                    \"badProperty\": false,\n                    \"numberProperty\": { \"minimum\": 10 }\n                }\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Vocabulary",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/draft2019-09/metaschema-no-validation.json\": {\n                    \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n                    \"$id\": \"http://localhost:1234/draft2019-09/metaschema-no-validation.json\",\n                    \"$vocabulary\": {\n                        \"https://json-schema.org/draft/2019-09/vocab/core\": true,\n                        \"https://json-schema.org/draft/2019-09/vocab/applicator\": true\n                    },\n                    \"$recursiveAnchor\": true,\n                    \"allOf\": [\n                        { \"$ref\": \"https://json-schema.org/draft/2019-09/meta/applicator\" },\n                        { \"$ref\": \"https://json-schema.org/draft/2019-09/meta/core\" }\n                    ]\n                }\n            }");
        }
    }
}

[TestCategory("Draft201909")]
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
                "tests/draft2019-09/vocabulary.json",
                "{\n                \"$schema\": \"http://localhost:1234/draft2019-09/metaschema-optional-vocabulary.json\",\n                \"type\": \"number\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft201909.Vocabulary",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/draft2019-09/metaschema-optional-vocabulary.json\": {\n                    \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n                    \"$id\": \"http://localhost:1234/draft2019-09/metaschema-optional-vocabulary.json\",\n                    \"$vocabulary\": {\n                        \"https://json-schema.org/draft/2019-09/vocab/core\": true,\n                        \"https://json-schema.org/draft/2019-09/vocab/validation\": true,\n                        \"http://localhost:1234/draft/2019-09/vocab/custom\": false\n                    },\n                    \"$recursiveAnchor\": true,\n                    \"allOf\": [\n                        { \"$ref\": \"https://json-schema.org/draft/2019-09/meta/validation\" },\n                        { \"$ref\": \"https://json-schema.org/draft/2019-09/meta/core\" }\n                    ]\n                }\n            }");
        }
    }
}
