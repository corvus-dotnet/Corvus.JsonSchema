using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft4.MinItems;

[TestCategory("Draft4")]
[TestClass]
public class SuiteMinItemsValidation
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
    public void TestLongerIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, 2]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExactLengthIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTooShortIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresNonArrays()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\minItems.json",
                "{\"minItems\": 1}",
                "JsonSchemaTestSuite.Draft4.MinItems",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
