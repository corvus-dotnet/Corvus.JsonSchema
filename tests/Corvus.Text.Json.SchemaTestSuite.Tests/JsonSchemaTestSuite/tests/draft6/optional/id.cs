using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft6.Optional.Id;

[TestCategory("Draft6")]
[TestClass]
public class SuiteIdInsideAnEnumIsNotARealIdentifier
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
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\n                    \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                    \"type\": \"null\"\n                }");
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
                "tests/draft6/optional/id.json",
                "{\n            \"definitions\": {\n                \"id_in_enum\": {\n                    \"enum\": [\n                        {\n                          \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                          \"type\": \"null\"\n                        }\n                    ]\n                },\n                \"real_id_in_schema\": {\n                    \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                    \"type\": \"string\"\n                },\n                \"zzz_id_in_const\": {\n                    \"const\": {\n                        \"$id\": \"https://localhost:1234/id/my_identifier.json\",\n                        \"type\": \"null\"\n                    }\n                }\n            },\n            \"anyOf\": [\n                { \"$ref\": \"#/definitions/id_in_enum\" },\n                { \"$ref\": \"https://localhost:1234/id/my_identifier.json\" }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft6.Optional.Id",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteNonSchemaObjectContainingAPlainNameIdProperty
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
    public void TestSkipTraversingDefinitionForAValidResult()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"skip not_a_real_anchor\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestConstAtConstNotAnchorDoesNotMatch()
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
                "tests/draft6/optional/id.json",
                "{\n            \"definitions\": {\n                \"const_not_anchor\": {\n                    \"const\": {\n                        \"$id\": \"#not_a_real_anchor\"\n                    }\n                }\n            },\n            \"oneOf\": [\n                {\n                    \"const\": \"skip not_a_real_anchor\"\n                },\n                {\n                    \"allOf\": [\n                        {\n                            \"not\": {\n                                \"const\": \"skip not_a_real_anchor\"\n                            }\n                        },\n                        {\n                            \"$ref\": \"#/definitions/const_not_anchor\"\n                        }\n                    ]\n                }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft6.Optional.Id",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteNonSchemaObjectContainingAnIdProperty
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
    public void TestSkipTraversingDefinitionForAValidResult()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"skip not_a_real_id\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestConstAtConstNotIdDoesNotMatch()
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
                "tests/draft6/optional/id.json",
                "{\n            \"definitions\": {\n                \"const_not_id\": {\n                    \"const\": {\n                        \"$id\": \"not_a_real_id\"\n                    }\n                }\n            },\n            \"oneOf\": [\n                {\n                    \"const\":\"skip not_a_real_id\"\n                },\n                {\n                    \"allOf\": [\n                        {\n                            \"not\": {\n                                \"const\": \"skip not_a_real_id\"\n                            }\n                        },\n                        {\n                            \"$ref\": \"#/definitions/const_not_id\"\n                        }\n                    ]\n                }\n            ]\n        }",
                "JsonSchemaTestSuite.Draft6.Optional.Id",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
