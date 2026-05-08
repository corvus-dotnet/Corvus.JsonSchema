using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.JsonPointer;

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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreFloats()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("13.7");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreBooleans()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllStringFormatsIgnoreNulls()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidJsonPointer()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/bar~0/baz~1/%a\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotAValidJsonPointerNotEscaped()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/bar~\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerWithEmptySegment()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo//bar\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerWithTheLastEmptySegment()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/bar/\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69011()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69012()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69013()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/0\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69014()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69015()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/a~1b\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69016()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/c%d\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69017()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/e^f\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69018()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/g|h\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc69019()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/i\\\\j\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc690110()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/k\\\"l\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc690111()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/ \"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerAsStatedInRfc690112()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/m~0n\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerUsedAddingToTheLastArrayPosition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/-\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerUsedAsObjectMemberName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/-/bar\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerMultipleEscapedCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~1~0~0~1~1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerEscapedWithFractionPart1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~1.1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerEscapedWithFractionPart2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~0.1\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotAValidJsonPointerUriFragmentIdentifier1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"#\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotAValidJsonPointerUriFragmentIdentifier2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"#/\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotAValidJsonPointerUriFragmentIdentifier3()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"#a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotAValidJsonPointerSomeEscapedButNotAll1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~0~\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotAValidJsonPointerSomeEscapedButNotAll2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~0/~\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotAValidJsonPointerWrongEscapeCharacter1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~2\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotAValidJsonPointerWrongEscapeCharacter2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~-1\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotAValidJsonPointerMultipleCharactersNotEscaped()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~~\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotAValidJsonPointerIsnTEmptyNorStartsWith1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotAValidJsonPointerIsnTEmptyNorStartsWith2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNotAValidJsonPointerIsnTEmptyNorStartsWith3()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a/a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerUnicodeCharactersAllowedByRfc6901()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/bar/😎\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidJsonPointerControlCharactersAllowedAfterJsonUnescaping()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo\\u0000bar\\n\\tbaz\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format\\json-pointer.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"json-pointer\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.JsonPointer",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
