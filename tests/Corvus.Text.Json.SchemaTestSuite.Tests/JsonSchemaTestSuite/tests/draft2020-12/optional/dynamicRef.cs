using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Optional.DynamicRef;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteDynamicRefSkipsOverIntermediateResourcesPointerReferenceAcrossResourceBoundary
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
    public void TestIntegerPropertyPasses()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"bar-item\": { \"content\": 42 } }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestStringPropertyFails()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"bar-item\": { \"content\": \"value\" } }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/optional/dynamicRef.json",
                "{\n        \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n        \"$id\": \"https://test.json-schema.org/dynamic-ref-skips-intermediate-resource/optional/main\",\n        \"type\": \"object\",\n          \"properties\": {\n              \"bar-item\": {\n                  \"$ref\": \"bar#/$defs/item\"\n              }\n          },\n          \"$defs\": {\n              \"bar\": {\n                  \"$id\": \"bar\",\n                  \"type\": \"array\",\n                  \"items\": {\n                      \"$ref\": \"item\"\n                  },\n                  \"$defs\": {\n                      \"item\": {\n                          \"$id\": \"item\",\n                          \"type\": \"object\",\n                          \"properties\": {\n                              \"content\": {\n                                  \"$dynamicRef\": \"#content\"\n                              }\n                          },\n                          \"$defs\": {\n                              \"defaultContent\": {\n                                  \"$dynamicAnchor\": \"content\",\n                                  \"type\": \"integer\"\n                              }\n                          }\n                      },\n                      \"content\": {\n                          \"$dynamicAnchor\": \"content\",\n                          \"type\": \"string\"\n                      }\n                  }\n              }\n          }\n      }",
                "JsonSchemaTestSuite.Draft202012.Optional.DynamicRef",
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
