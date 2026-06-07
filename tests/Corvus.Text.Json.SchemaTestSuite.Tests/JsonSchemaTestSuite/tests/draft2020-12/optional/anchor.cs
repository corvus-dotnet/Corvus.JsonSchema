using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Optional.Anchor;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteAnchorInsideAnEnumIsNotARealIdentifier
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
    public void TestExactMatchToEnumAndTypeMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"$anchor\": \"my_anchor\",\n                    \"type\": \"null\"\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInImplementationsThatStripAnchorThisMayMatchEitherDef()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"type\": \"null\"\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatchRefToAnchor()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a string to match #/$defs/anchor_in_enum\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoMatchOnEnumOrRefToAnchor()
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
                "tests/draft2020-12/optional/anchor.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"$defs\": {\n                \"anchor_in_enum\": {\n                    \"enum\": [\n                        {\n                            \"$anchor\": \"my_anchor\",\n                            \"type\": \"null\"\n                        }\n                    ]\n                },\n                \"real_identifier_in_schema\": {\n                    \"$anchor\": \"my_anchor\",\n                    \"type\": \"string\"\n                },\n                \"zzz_anchor_in_const\": {\n                    \"const\": {\n                        \"$anchor\": \"my_anchor\",\n                        \"type\": \"null\"\n                    }\n                }\n            },\n            \"anyOf\": [\n                { \"$ref\": \"#/$defs/anchor_in_enum\" },\n                { \"$ref\": \"#my_anchor\" }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Anchor",
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
