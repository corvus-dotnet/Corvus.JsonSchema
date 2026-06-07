using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft7.Optional.UnknownKeyword;

[TestCategory("Draft7")]
[TestClass]
public class SuiteIdInsideAnUnknownKeywordIsNotARealIdentifier
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
    public void TestTypeMatchesSecondAnyOfWhichHasARealSchemaInIt()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a string\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTypeMatchesNonSchemaInFirstAnyOf()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("null");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTypeMatchesNonSchemaInThirdAnyOf()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft7/optional/unknownKeyword.json",
                "{\n            \"definitions\": {\n                \"id_in_unknown0\": {\n                    \"not\": {\n                        \"array_of_schemas\": [\n                            {\n                              \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\n                              \"type\": \"null\"\n                            }\n                        ]\n                    }\n                },\n                \"real_id_in_schema\": {\n                    \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\n                    \"type\": \"string\"\n                },\n                \"id_in_unknown1\": {\n                    \"not\": {\n                        \"object_of_schemas\": {\n                            \"foo\": {\n                              \"$id\": \"https://localhost:1234/unknownKeyword/my_identifier.json\",\n                              \"type\": \"integer\"\n                            }\n                        }\n                    }\n                }\n            },\n            \"anyOf\": [\n                { \"$ref\": \"#/definitions/id_in_unknown0\" },\n                { \"$ref\": \"#/definitions/id_in_unknown1\" },\n                { \"$ref\": \"https://localhost:1234/unknownKeyword/my_identifier.json\" }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft7.Optional.UnknownKeyword",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
