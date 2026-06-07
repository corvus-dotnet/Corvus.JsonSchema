using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft201909.Optional.NonBmpRegex;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteProperUtf16SurrogatePairHandlingPattern
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
    public void TestMatchesEmpty()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatchesSingle()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"🐲\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatchesTwo()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"🐲🐲\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDoesnTMatchOne()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"🐉\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDoesnTMatchTwo()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"🐉🐉\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDoesnTMatchOneAscii()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"D\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDoesnTMatchTwoAscii()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"DD\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/optional/non-bmp-regex.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"pattern\": \"^🐲*$\"\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.NonBmpRegex",
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
public class SuiteProperUtf16SurrogatePairHandlingPatternProperties
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
    public void TestMatchesEmpty()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatchesSingle()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"🐲\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatchesTwo()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"🐲🐲\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDoesnTMatchOne()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"🐲\": \"hello\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDoesnTMatchTwo()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"🐲🐲\": \"hello\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/optional/non-bmp-regex.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"patternProperties\": {\n                \"^🐲*$\": {\n                    \"type\": \"integer\"\n                }\n            }\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.NonBmpRegex",
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
