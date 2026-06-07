using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Optional.CrossDraft;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts
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
    public void TestFirstItemNotAStringIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, 2, 3]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/optional/cross-draft.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"type\": \"array\",\n            \"$ref\": \"http://localhost:1234/draft2019-09/ignore-prefixItems.json\"\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.CrossDraft",
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
