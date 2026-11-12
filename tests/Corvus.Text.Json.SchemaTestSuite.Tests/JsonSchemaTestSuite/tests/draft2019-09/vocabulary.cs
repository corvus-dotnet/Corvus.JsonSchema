using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft201909.Vocabulary;

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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"badProperty\": \"this property should not exist\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoValidationValidNumber()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"numberProperty\": 20 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoValidationInvalidNumberButItStillValidates()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"numberProperty\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/vocabulary.json",
                "{\n                \"$schema\": \"http://localhost:1234/draft2019-09/metaschema-no-validation.json\",\n                \"properties\": {\n                    \"badProperty\": false,\n                    \"numberProperty\": { \"minimum\": 10 }\n                }\n            }",
                "JsonSchemaTestSuite.Draft201909.Vocabulary",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foobar\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNumberValue()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("20");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/vocabulary.json",
                "{\n                \"$schema\": \"http://localhost:1234/draft2019-09/metaschema-optional-vocabulary.json\",\n                \"type\": \"number\"\n            }",
                "JsonSchemaTestSuite.Draft201909.Vocabulary",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly(),
                externalSchemas: "{\n                \"http://localhost:1234/draft2019-09/metaschema-optional-vocabulary.json\": {\n                    \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n                    \"$id\": \"http://localhost:1234/draft2019-09/metaschema-optional-vocabulary.json\",\n                    \"$vocabulary\": {\n                        \"https://json-schema.org/draft/2019-09/vocab/core\": true,\n                        \"https://json-schema.org/draft/2019-09/vocab/validation\": true,\n                        \"http://localhost:1234/draft/2019-09/vocab/custom\": false\n                    },\n                    \"$recursiveAnchor\": true,\n                    \"allOf\": [\n                        { \"$ref\": \"https://json-schema.org/draft/2019-09/meta/validation\" },\n                        { \"$ref\": \"https://json-schema.org/draft/2019-09/meta/core\" }\n                    ]\n                }\n            }");
        }
    }
}
