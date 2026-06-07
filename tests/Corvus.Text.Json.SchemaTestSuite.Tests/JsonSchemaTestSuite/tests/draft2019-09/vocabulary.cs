using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft201909.Vocabulary;

[TestCategory("Draft201909")]
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"badProperty\": \"this property should not exist\"\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoValidationValidNumber()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"numberProperty\": 20\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoValidationInvalidNumberButItStillValidates()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"numberProperty\": 1\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/vocabulary.json",
                "{\n            \"$id\": \"https://schema/using/no/validation\",\n            \"$schema\": \"http://localhost:1234/draft2019-09/metaschema-no-validation.json\",\n            \"properties\": {\n                \"badProperty\": false,\n                \"numberProperty\": {\n                    \"minimum\": 10\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Vocabulary",
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
                "{\n             \"$schema\": \"http://localhost:1234/draft2019-09/metaschema-optional-vocabulary.json\",\n             \"type\": \"number\"\n         }",
                "JsonSchemaTestSuite.Draft201909.Vocabulary",
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
