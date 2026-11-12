using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft4.Optional.NonSchemas;

[TestCategory("Draft4")]
[TestClass]
public class SuiteIdInsideAnEnumIsNotARealIdentifierIdDefinitions
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                        \"id\": \"https://localhost:1234/my_identifier.json\",\n                        \"type\": \"null\"\n                    }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatchRefToId()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a string to match #/definitions/id_in_enum\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoMatchOnEnumOrRefToId()
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
                "tests/draft4/optional/non-schemas.json",
                "{\n                \"definitions\": {\n                    \"id_in_enum\": {\n                        \"enum\": [\n                            {\n                                \"id\": \"https://localhost:1234/my_identifier.json\",\n                                \"type\": \"null\"\n                            }\n                        ]\n                    },\n                    \"real_id_in_schema\": {\n                        \"id\": \"https://localhost:1234/my_identifier.json\",\n                        \"type\": \"string\"\n                    },\n                    \"zzz_id_in_const\": {\n                        \"enum\": [\n                            {\n                                \"id\": \"https://localhost:1234/my_identifier.json\",\n                                \"type\": \"null\"\n                            }\n                        ]\n                    }\n                },\n                \"anyOf\": [\n                    { \"$ref\": \"#/definitions/id_in_enum\" },\n                    { \"$ref\": \"https://localhost:1234/my_identifier.json\" }\n                ]\n            }",
                "JsonSchemaTestSuite.Draft4.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
[TestClass]
public class SuiteAnchorInsideAnEnumIsNotARealIdentifierId
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                        \"id\": \"#my_anchor\",\n                        \"type\": \"null\"\n                    }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestImplementationsThatStripIdMayMatchEitherDefinition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                        \"type\": \"null\"\n                    }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatchRefToAnchor()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a string to match #/definitions/anchor_in_enum\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoMatchOnEnumOrRefToId()
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
                "tests/draft4/optional/non-schemas.json",
                "{\n                \"definitions\": {\n                    \"anchor_in_enum\": {\n                        \"enum\": [\n                            {\n                                \"id\": \"#my_anchor\",\n                                \"type\": \"null\"\n                            }\n                        ]\n                    },\n                    \"real_identifier_in_schema\": {\n                        \"id\": \"#my_anchor\",\n                        \"type\": \"string\"\n                    },\n                    \"zzz_anchor_in_const\": {\n                        \"enum\": [\n                            {\n                                \"id\": \"#my_anchor\",\n                                \"type\": \"null\"\n                            }\n                        ]\n                    }\n                },\n                \"anyOf\": [\n                    { \"$ref\": \"#/definitions/anchor_in_enum\" },\n                    { \"$ref\": \"#my_anchor\" }\n                ]\n            }",
                "JsonSchemaTestSuite.Draft4.Optional.NonSchemas",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
