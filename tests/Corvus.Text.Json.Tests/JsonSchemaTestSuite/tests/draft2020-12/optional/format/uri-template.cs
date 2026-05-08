using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Optional.Format.UriTemplate;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteFormatUriTemplate
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
    public void TestAllStringFormatsIgnoreIntegers()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("12");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreFloats()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("13.7");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreObjects()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreArrays()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreBooleans()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("false");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreNulls()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("null");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUriTemplate()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://example.com/dictionary/{term:1}/{term}\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidUriTemplate()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://example.com/dictionary/{term:1}/{term\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUriTemplateWithoutVariables()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://example.com/dictionary\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidRelativeUriTemplate()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"dictionary/{term:1}/{term}\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format\\uri-template.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"uri-template\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.UriTemplate",
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
