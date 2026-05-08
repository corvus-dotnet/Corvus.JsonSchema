using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.UriReference;

[TestCategory("Draft7")]
[TestClass]
public class SuiteValidationOfUriReferences
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
    public void TestAValidUri()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://foo.bar/?baz=qux#quux\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidProtocolRelativeUriReference()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"//foo.bar/?baz=qux#quux\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidRelativeUriReference()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/abc\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidUriReference()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\\\\\\WINDOWS\\\\fileshare\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUriReference()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"abc\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUriFragment()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"#fragment\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidUriFragment()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"#frag\\\\ment\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnescapedNonUsAsciiCharacters()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foobar®.txt\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidBackslashCharacter()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"https://example.org/foobar\\\\.txt\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUriWithLeadingZeroIPv4IsStructurallyValidAsARegName()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://087.10.0.1/\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUriWithOutOfBoundsIPv4IsStructurallyValidAsARegName()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"http://999.999.999.999/\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\optional\\format\\uri-reference.json",
                "{ \"format\": \"uri-reference\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.UriReference",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
