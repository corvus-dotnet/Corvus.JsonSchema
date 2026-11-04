using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft201909.FormatRelativeJsonPointer;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteValidationOfNonStringRelativeJsonPointerValues
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

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/format-relative-json-pointer.json",
                "{\n                \"format\": \"relative-json-pointer\"\n            }",
                "JsonSchemaTestSuite.Draft201909.FormatRelativeJsonPointer",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
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
public class SuiteRelativeJsonPointerFormat
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
    public void TestInvalidRelativeJsonPointerStringIsOnlyAnAnnotationByDefault()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foo/bar\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/format-relative-json-pointer.json",
                "{\n                \"format\": \"relative-json-pointer\"\n            }",
                "JsonSchemaTestSuite.Draft201909.FormatRelativeJsonPointer",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
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
public class SuiteValidationOfRelativeJsonPointersRjp
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
    public void TestAValidUpwardsRjp()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidDownwardsRjp()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0/foo/bar\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidUpAndThenDownRjpWithArrayIndex()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"2/0/baz/1/zip\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAValidRjpTakingTheMemberOrIndexName()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0#\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAnInvalidRjpThatIsAValidJsonPointer()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"/foo/bar\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAJsonPointerPartThatDoesNotStartWithASlashIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1foo\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNegativePrefix()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"-1/foo/bar\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestExplicitPositivePrefix()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"+1/foo/bar\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonAsciiDigitInThePrefixIsNotAllowed()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"١/foo\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIsNotAValidJsonPointer()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0##\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroCannotBeFollowedByOtherDigitsPlusJsonPointer()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"01/a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroCannotBeFollowedByOtherDigitsPlusOctothorpe()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"01#\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmptyString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMultiDigitIntegerPrefix()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"120/foo/bar\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMultiDigitPrefixWithAZeroFollowedByAnotherDigit()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"100\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTildeNotFollowedBy0Or1InTheJsonPointerPart()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0/~2\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnescapedTildeAtTheEndOfTheJsonPointerPart()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0/foo/bar~\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestOctothorpeFollowedByAJsonPointer()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1#/foo/bar\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmptyReferenceTokensInTheJsonPointerPart()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0//\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidEscapedSlash1InTheJsonPointerPart()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0/a~1b\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestValidEscapedTilde0InTheJsonPointerPart()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0/m~0n\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMultipleEscapedCharactersInTheJsonPointerPart()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0/~1~0~0~1~1\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUsedAsAMemberNameInTheJsonPointerPart()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1/foo/-\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMultipleUnescapedTildesInTheJsonPointerPart()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0/~~\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTrailingNewlineAfterTheNonNegativeInteger()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"1\\n\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests/draft2019-09/format-relative-json-pointer.json",
                "{\n                \"format\": \"relative-json-pointer\"\n            }",
                "JsonSchemaTestSuite.Draft201909.FormatRelativeJsonPointer",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
