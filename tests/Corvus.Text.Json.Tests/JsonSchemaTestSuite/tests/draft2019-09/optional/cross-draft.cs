using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft201909.Optional.CrossDraft;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteRefsToFutureDraftsAreProcessedAsFutureDrafts
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
    public void TestFirstItemNotAStringIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[1, 2, 3]");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFirstItemIsAStringIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[\"a string\", 1, 2, 3]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\cross-draft.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"array\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/prefixItems.json\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.CrossDraft",
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
public class SuiteRefsToHistoricDraftsAreProcessedAsHistoricDrafts
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
    public void TestMissingBarIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": \"any value\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\cross-draft.json",
                "{\r\n            \"type\": \"object\",\r\n            \"allOf\": [\r\n                { \"properties\": { \"foo\": true } },\r\n                { \"$ref\": \"http://localhost:1234/draft7/ignore-dependentRequired.json\" }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.CrossDraft",
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
