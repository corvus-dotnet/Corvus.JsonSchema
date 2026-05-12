using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft6.Optional.NonBmpRegex;

[TestCategory("Draft6")]
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft6\\optional\\non-bmp-regex.json",
                "{ \"pattern\": \"^🐲*$\" }",
                "JsonSchemaTestSuite.Draft6.Optional.NonBmpRegex",
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
        (s_fixture as IDisposable)?.Dispose();
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
                "tests\\draft6\\optional\\non-bmp-regex.json",
                "{\r\n            \"patternProperties\": {\r\n                \"^🐲*$\": {\r\n                    \"type\": \"integer\"\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft6.Optional.NonBmpRegex",
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
