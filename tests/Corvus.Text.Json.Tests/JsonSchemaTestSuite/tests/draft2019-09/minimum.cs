using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft201909.Minimum;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteMinimumValidation
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
    public void TestAboveTheMinimumIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("2.6");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBoundaryPointIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1.1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBelowTheMinimumIsInvalid()
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
                "tests\\draft2019-09\\minimum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"minimum\": 1.1\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Minimum",
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
public class SuiteMinimumValidationWithSignedInteger
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
    public void TestNegativeAboveTheMinimumIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("-1");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestPositiveAboveTheMinimumIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("0");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBoundaryPointIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("-2");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBoundaryPointWithFloatIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("-2.0");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFloatBelowTheMinimumIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("-2.0001");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIntBelowTheMinimumIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("-3");
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
                "tests\\draft2019-09\\minimum.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"minimum\": -2\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Minimum",
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
