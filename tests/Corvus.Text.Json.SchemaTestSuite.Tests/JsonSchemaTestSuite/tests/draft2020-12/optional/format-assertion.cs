using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Optional.FormatAssertion;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionFalse
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
    public void TestFormatAssertionFalseValidString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"127.0.0.1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFormatAssertionFalseInvalidString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"not-an-ipv4\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/optional/format-assertion.json",
                "{\n            \"$id\": \"https://schema/using/format-assertion/false\",\n            \"$schema\": \"http://localhost:1234/draft2020-12/format-assertion-false.json\",\n            \"format\": \"ipv4\"\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.FormatAssertion",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteSchemaThatUsesCustomMetaschemaWithFormatAssertionTrue
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
    public void TestFormatAssertionTrueValidString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"127.0.0.1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFormatAssertionTrueInvalidString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"not-an-ipv4\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/optional/format-assertion.json",
                "{\n            \"$id\": \"https://schema/using/format-assertion/true\",\n            \"$schema\": \"http://localhost:1234/draft2020-12/format-assertion-true.json\",\n            \"format\": \"ipv4\"\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.FormatAssertion",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
