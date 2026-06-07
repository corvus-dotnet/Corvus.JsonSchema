using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.ExclusiveMinimum;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteExclusiveMinimumValidation
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
    public void TestAboveTheExclusiveMinimumIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1.2");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBoundaryPointIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1.1");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBelowTheExclusiveMinimumIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("0.6");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresNonNumbers()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"x\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/exclusiveMinimum.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"exclusiveMinimum\": 1.1\n        }",
                "JsonSchemaTestSuite.Draft202012.ExclusiveMinimum",
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
