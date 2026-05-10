using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.Optional.Format.JsonPointer;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidationOfJsonPointersJsonStringRepresentation
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
    public void TestAValidJsonPointer()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foo/bar~0/baz~1/%a\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotAValidJsonPointerNotEscaped()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foo/bar~\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerWithEmptySegment()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foo//bar\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerWithTheLastEmptySegment()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foo/bar/\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69011()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69012()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foo\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69013()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foo/0\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69014()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69015()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/a~1b\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69016()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/c%d\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69017()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/e^f\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69018()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/g|h\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69019()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/i\\\\j\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc690110()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/k\\\"l\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc690111()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/ \"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc690112()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/m~0n\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerUsedAddingToTheLastArrayPosition()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foo/-\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerUsedAsObjectMemberName()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foo/-/bar\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerMultipleEscapedCharacters()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/~1~0~0~1~1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerEscapedWithFractionPart1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/~1.1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerEscapedWithFractionPart2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/~0.1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotAValidJsonPointerUriFragmentIdentifier1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"#\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotAValidJsonPointerUriFragmentIdentifier2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"#/\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotAValidJsonPointerUriFragmentIdentifier3()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"#a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotAValidJsonPointerSomeEscapedButNotAll1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/~0~\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotAValidJsonPointerSomeEscapedButNotAll2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/~0/~\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotAValidJsonPointerWrongEscapeCharacter1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/~2\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotAValidJsonPointerWrongEscapeCharacter2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/~-1\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotAValidJsonPointerMultipleCharactersNotEscaped()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/~~\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotAValidJsonPointerIsnTEmptyNorStartsWith1()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotAValidJsonPointerIsnTEmptyNorStartsWith2()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNotAValidJsonPointerIsnTEmptyNorStartsWith3()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a/a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerUnicodeCharactersAllowedByRfc6901()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foo/bar/😎\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidJsonPointerControlCharactersAllowedAfterJsonUnescaping()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foo\\u0000bar\\n\\tbaz\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\format\\json-pointer.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"json-pointer\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.Format.JsonPointer",
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
