using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.FormatRegex;

[TestCategory("Draft7")]
[TestClass]
public class SuiteAIsNotAnEcma262ControlEscape
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
    public void TestWhenUsedAsAPattern()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/optional/format-regex.json",
                "{\n                \"format\": \"regex\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.FormatRegex",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuitePythonSpecificRegularExpressionSyntaxIsNotValidEcma262
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
    public void TestPythonNamedGroupPNameIsNotEcma262()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"(?P<name>x)\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestPythonNamedBackreferencePNameIsNotEcma262()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"(?P<n>a)(?P=n)\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInlineCommentGroupIsNotEcma262()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"(?#comment)a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/optional/format-regex.json",
                "{\n                \"format\": \"regex\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.FormatRegex",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteGlobalInlineFlagGroupsAreNotValidEcma262
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
    public void TestASingleGlobalInlineFlagI()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"(?i)abc\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMultipleGlobalInlineFlagsIms()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"(?ims)abc\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/optional/format-regex.json",
                "{\n                \"format\": \"regex\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.FormatRegex",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteEcma262NamedGroupsAndBackreferencesAreValid
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
    public void TestAnEcma262NamedGroupName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"(?<name>x)\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAnEcma262NamedBackreferenceKName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"(?<n>a)\\\\k<n>\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/optional/format-regex.json",
                "{\n                \"format\": \"regex\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.FormatRegex",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteEcma262LookbehindIsValidIncludingVariableWidth
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
    public void TestAVariableWidthLookbehindEs2018()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"(?<=a+)b\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/optional/format-regex.json",
                "{\n                \"format\": \"regex\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.FormatRegex",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft7")]
[TestClass]
public class SuiteEcma262CharacterClassesAndEscapes
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
    public void TestAnEmptyCharacterClassIsValidEcma262()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"[]\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestANegatedEmptyCharacterClassIsValidEcma262()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"[^]\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAControlEscapeCaIsValidEcma262()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\cA\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft7/optional/format-regex.json",
                "{\n                \"format\": \"regex\"\n            }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.FormatRegex",
                "../../../../../JSON-Schema-Test-Suite/validation/tests",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
