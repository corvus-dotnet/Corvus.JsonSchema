using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Optional.Format.Uuid;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteUuidFormat
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
    public void TestAllUpperCase()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2EB8AA08-AA98-11EA-B4AA-73B441D16380\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllLowerCase()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2eb8aa08-aa98-11ea-b4aa-73b441d16380\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMixedCase()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2eb8aa08-AA98-11ea-B4Aa-73B441D16380\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllZeroesIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"00000000-0000-0000-0000-000000000000\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWrongLength()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2eb8aa08-aa98-11ea-b4aa-73b441d1638\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMissingSection()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2eb8aa08-aa98-11ea-73b441d16380\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBadCharactersNotHex()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2eb8aa08-aa98-11ea-b4ga-73b441d16380\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNoDashes()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2eb8aa08aa9811eab4aa73b441d16380\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTooFewDashes()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2eb8aa08aa98-11ea-b4aa73b441d16380\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTooManyDashes()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2eb8-aa08-aa98-11ea-b4aa73b44-1d16380\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDashesInTheWrongSpot()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2eb8aa08aa9811eab4aa73b441d16380----\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestShiftedDashes()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2eb8aa0-8aa98-11e-ab4aa7-3b441d16380\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidVersion4()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"98d80576-482e-427f-8434-7f86890ab222\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidVersion5()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"99c17cbb-656f-564a-940f-1a4568f03487\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHypotheticalVersion6()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"99c17cbb-656f-664a-940f-1a4568f03487\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestHypotheticalVersion15()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"99c17cbb-656f-f64a-940f-1a4568f03487\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2020-12/optional/format/uuid.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n            \"format\": \"uuid\"\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.Uuid",
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
