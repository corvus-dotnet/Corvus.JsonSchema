using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft202012.IfThenElse;

[TestCategory("Draft202012")]
[TestClass]
public class SuiteIgnoreIfWithoutThenOrElse
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
    public void TestValidWhenValidAgainstLoneIf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidWhenInvalidAgainstLoneIf()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"const\": 0\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteIgnoreThenWithoutIf
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
    public void TestValidWhenValidAgainstLoneThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidWhenInvalidAgainstLoneThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"then\": {\r\n                \"const\": 0\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteIgnoreElseWithoutIf
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
    public void TestValidWhenValidAgainstLoneElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("0");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidWhenInvalidAgainstLoneElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"else\": {\r\n                \"const\": 0\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteIfAndThenWithoutElse
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
    public void TestValidThroughThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidThroughThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-100");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidWhenIfTestFails()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"exclusiveMaximum\": 0\r\n            },\r\n            \"then\": {\r\n                \"minimum\": -10\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteIfAndElseWithoutThen
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
    public void TestValidWhenIfTestPasses()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidThroughElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("4");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidThroughElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"exclusiveMaximum\": 0\r\n            },\r\n            \"else\": {\r\n                \"multipleOf\": 2\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteValidateAgainstCorrectBranchThenVsElse
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
    public void TestValidThroughThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidThroughThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-100");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidThroughElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("4");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidThroughElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"exclusiveMaximum\": 0\r\n            },\r\n            \"then\": {\r\n                \"minimum\": -10\r\n            },\r\n            \"else\": {\r\n                \"multipleOf\": 2\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteNonInterferenceAcrossCombinedSchemas
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
    public void TestValidButWouldHaveBeenInvalidThroughThen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("-100");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestValidButWouldHaveBeenInvalidThroughElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("3");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"if\": {\r\n                        \"exclusiveMaximum\": 0\r\n                    }\r\n                },\r\n                {\r\n                    \"then\": {\r\n                        \"minimum\": -10\r\n                    }\r\n                },\r\n                {\r\n                    \"else\": {\r\n                        \"multipleOf\": 2\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteIfWithBooleanSchemaTrue
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
    public void TestBooleanSchemaTrueInIfAlwaysChoosesTheThenPathValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"then\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBooleanSchemaTrueInIfAlwaysChoosesTheThenPathInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"else\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": true,\r\n            \"then\": { \"const\": \"then\" },\r\n            \"else\": { \"const\": \"else\" }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteIfWithBooleanSchemaFalse
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
    public void TestBooleanSchemaFalseInIfAlwaysChoosesTheElsePathInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"then\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBooleanSchemaFalseInIfAlwaysChoosesTheElsePathValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"else\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": false,\r\n            \"then\": { \"const\": \"then\" },\r\n            \"else\": { \"const\": \"else\" }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteIfAppearsAtTheEndWhenSerializedKeywordProcessingSequence
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
    public void TestYesRedirectsToThenAndPasses()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"yes\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestOtherRedirectsToElseAndPasses()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"other\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNoRedirectsToThenAndFails()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"no\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidRedirectsToElseAndFails()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"invalid\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"then\": { \"const\": \"yes\" },\r\n            \"else\": { \"const\": \"other\" },\r\n            \"if\": { \"maxLength\": 4 }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteThenFalseFailsWhenConditionMatches
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
    public void TestMatchesIfThenFalseInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDoesNotMatchIfThenIgnoredValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"if\": { \"const\": 1 },\r\n            \"then\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft202012")]
[TestClass]
public class SuiteElseFalseFailsWhenConditionDoesNotMatch
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
    public void TestMatchesIfElseIgnoredValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDoesNotMatchIfElseExecutesInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("2");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\if-then-else.json",
                "{\r\n            \"if\": { \"const\": 1 },\r\n            \"else\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.IfThenElse",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
