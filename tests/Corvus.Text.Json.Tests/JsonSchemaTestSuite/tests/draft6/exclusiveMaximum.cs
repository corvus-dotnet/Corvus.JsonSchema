using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft6.ExclusiveMaximum;

[TestCategory("Draft6")]
[TestClass]
public class SuiteExclusiveMaximumValidation
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestBelowTheExclusiveMaximumIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("2.2");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBoundaryPointIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("3.0");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAboveTheExclusiveMaximumIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("3.5");
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
                "tests\\draft6\\exclusiveMaximum.json",
                "{\r\n            \"exclusiveMaximum\": 3.0\r\n        }",
                "JsonSchemaTestSuite.Draft6.ExclusiveMaximum",
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
