using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.FormatUriTemplate;

[TestCategory("Draft7")]
[TestClass]
public class SuiteValidationOfNonStringUriTemplateValues
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

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/format-uri-template.json",
                "{\n                \"format\": \"uri-template\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.FormatUriTemplate",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteValidationOfUriTemplates
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
    public void TestAValidUriTemplate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com/dictionary/{term:1}/{term}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnInvalidUriTemplate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com/dictionary/{term:1}/{term\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidUriTemplateWithoutVariables()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://example.com/dictionary\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAValidRelativeUriTemplate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"dictionary/{term:1}/{term}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnEmptyExpressionIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnEmptyVarspecInsideTheVariableListIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{a,,b}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAZeroPrefixMaxLengthIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{v:0}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAFiveDigitPrefixMaxLengthIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{v:10000}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnEmptyStringIsAValidUriTemplate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnUnmatchedClosingBraceIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo}bar\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAPercentEncodedTripletInALiteralIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a%41b\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestALiteralWithALonePercentSignIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a%\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestALiteralWithAnIncompletePercentEncodedTripletIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a%4\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestALiteralWithNonHexPercentEncodingIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a%GG\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestASupplementaryPlaneCharacterInALiteralIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\ud83d\\ude00b\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAPrivateUseCharacterInALiteralIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\ue000b\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestASupplementaryPrivateUseCharacterInALiteralIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\udb80\\udc00b\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestReservedExpansionOperator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{+var}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFragmentExpansionOperator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{#var}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLabelExpansionOperator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{.var}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPathSegmentExpansionOperator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{/var}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPathStyleParameterExpansionOperator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{;var}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFormStyleQueryExpansionOperator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{?var}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFormStyleQueryContinuationOperator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{&var}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestTheExplodeModifierIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{var*}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCombinedPrefixAndExplodeModifiersAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{var:1*}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAVariableListWithMoreThanOneVarspecIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{x,y}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADottedVariableNameIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{a.b}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestATrailingDotInAVariableNameIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{a.}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADoubleDotInAVariableNameIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{a..b}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAVariableNameMayStartWithAPercentEncodedTriplet()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{%41}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAVariableNameWithALonePercentSignIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{%}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAVariableNameWithAnIncompletePercentEncodedTripletIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{%4}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAVariableNameWithNonHexPercentEncodingIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{%GG}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAFourDigitPrefixMaxLengthIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{v:1000}\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestALeadingZeroInThePrefixMaxLengthIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{v:01}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestASpaceInALiteralIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a b\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADeleteCharacterInALiteralIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\u007fb\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestATrailingCommaInTheVariableListIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{a,}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADefaultValueInAVarspecIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{var=def}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnOperatorInPlaceOfTheFirstVarspecIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"{,+var}\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnApostropheInALiteralIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a'b\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/format-uri-template.json",
                "{\n                \"format\": \"uri-template\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.FormatUriTemplate",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
