using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex;

[TestCategory("Draft6")]
[TestClass]
public class SuiteEcma262RegexDoesNotMatchTrailingNewline
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
    public void TestMatchesInPythonButNotInEcma262()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abc\\\\n\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abc\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"string\",\n            \"pattern\": \"^abc$\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteEcma262RegexConvertsTToHorizontalTab
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
    public void TestDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\t\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0009\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"string\",\n            \"pattern\": \"^\\\\t$\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteEcma262RegexEscapesControlCodesWithCAndUpperLetter
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
    public void TestDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\cC\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0003\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"string\",\n            \"pattern\": \"^\\\\cC$\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteEcma262RegexEscapesControlCodesWithCAndLowerLetter
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
    public void TestDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\cc\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0003\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"string\",\n            \"pattern\": \"^\\\\cc$\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteEcma262DMatchesAsciiDigitsOnly
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
    public void TestAsciiZeroMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNkoDigitZeroDoesNotMatchUnlikeEGPython()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"߀\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNkoDigitZeroAsUEscapeDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u07c0\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"string\",\n            \"pattern\": \"^\\\\d$\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteEcma262DMatchesEverythingButAsciiDigits
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
    public void TestAsciiZeroDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNkoDigitZeroMatchesUnlikeEGPython()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"߀\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNkoDigitZeroAsUEscapeMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u07c0\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"string\",\n            \"pattern\": \"^\\\\D$\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteEcma262WMatchesAsciiLettersOnly
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
    public void TestAsciiAMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLatin1EAcuteDoesNotMatchUnlikeEGPython()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"é\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"string\",\n            \"pattern\": \"^\\\\w$\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteEcma262WMatchesEverythingButAsciiLetters
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
    public void TestAsciiADoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLatin1EAcuteMatchesUnlikeEGPython()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"é\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"string\",\n            \"pattern\": \"^\\\\W$\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteEcma262SMatchesWhitespace
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
    public void TestAsciiSpaceMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\" \"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCharacterTabulationMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\t\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLineTabulationMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u000b\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFormFeedMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u000c\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLatin1NonBreakingSpaceMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u00a0\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroWidthWhitespaceMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\ufeff\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLineFeedMatchesLineTerminator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u000a\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestParagraphSeparatorMatchesLineTerminator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u2029\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEmSpaceMatchesSpaceSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u2003\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonWhitespaceControlDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0001\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonWhitespaceDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u2013\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"string\",\n            \"pattern\": \"^\\\\s$\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteEcma262SMatchesEverythingButWhitespace
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
    public void TestAsciiSpaceDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\" \"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCharacterTabulationDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\t\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLineTabulationDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u000b\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestFormFeedDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u000c\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLatin1NonBreakingSpaceDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u00a0\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestZeroWidthWhitespaceDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\ufeff\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLineFeedDoesNotMatchLineTerminator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u000a\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestParagraphSeparatorDoesNotMatchLineTerminator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u2029\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestEmSpaceDoesNotMatchSpaceSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u2003\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonWhitespaceControlMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0001\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonWhitespaceMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u2013\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"string\",\n            \"pattern\": \"^\\\\S$\"\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuitePatternsAlwaysUseUnicodeSemanticsWithPattern
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
    public void TestAsciiCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'\\u00e9cole, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"LES HIVERS DE MON ENFANCE ÉTAIENT DES SAISONS LONGUES, LONGUES. NOUS VIVIONS EN TROIS LIEUX: L'ÉCOLE, L'ÉGLISE ET LA PATINOIRE; MAIS LA VRAIE VIE ÉTAIT SUR LA PATINOIRE.\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{ \"pattern\": \"\\\\p{Letter}cole\" }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteWInPatternsMatchesAZaZ09NotUnicodeLetters
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
    public void TestAsciiCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'\\u00e9cole, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"LES HIVERS DE MON ENFANCE ÉTAIENT DES SAISONS LONGUES, LONGUES. NOUS VIVIONS EN TROIS LIEUX: L'ÉCOLE, L'ÉGLISE ET LA PATINOIRE; MAIS LA VRAIE VIE ÉTAIT SUR LA PATINOIRE.\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{ \"pattern\": \"\\\\wcole\" }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuitePatternWithAsciiRanges
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
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'\\u00e9cole, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAsciiCharactersMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{ \"pattern\": \"[a-z]cole\" }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteDInPatternMatches09NotUnicodeDigits
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
    public void TestAsciiDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"42\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAsciiNonDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-%#\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"৪২\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{ \"pattern\": \"^\\\\d+$\" }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuitePatternWithNonAsciiDigits
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
    public void TestAsciiDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"42\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAsciiNonDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-%#\"");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"৪২\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{ \"pattern\": \"^\\\\p{digit}+$\" }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuitePatternsAlwaysUseUnicodeSemanticsWithPatternProperties
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
    public void TestAsciiCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'ecole\": \"pas de vraie vie\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'école\": \"pas de vraie vie\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'\\u00e9cole\": \"pas de vraie vie\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"L'ÉCOLE\": \"PAS DE VRAIE VIE\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"object\",\n            \"patternProperties\": {\n                \"\\\\p{Letter}cole\": true\n            },\n            \"additionalProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteWInPatternPropertiesMatchesAZaZ09NotUnicodeLetters
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
    public void TestAsciiCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'ecole\": \"pas de vraie vie\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'école\": \"pas de vraie vie\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'\\u00e9cole\": \"pas de vraie vie\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"L'ÉCOLE\": \"PAS DE VRAIE VIE\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"object\",\n            \"patternProperties\": {\n                \"\\\\wcole\": true\n            },\n            \"additionalProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuitePatternPropertiesWithAsciiRanges
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
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'école\": \"pas de vraie vie\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'\\u00e9cole\": \"pas de vraie vie\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAsciiCharactersMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'ecole\": \"pas de vraie vie\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"object\",\n            \"patternProperties\": {\n                \"[a-z]cole\": true\n            },\n            \"additionalProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuiteDInPatternPropertiesMatches09NotUnicodeDigits
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
    public void TestAsciiDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"42\": \"life, the universe, and everything\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAsciiNonDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"-%#\": \"spending the year dead for tax reasons\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"৪২\": \"khajit has wares if you have coin\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"object\",\n            \"patternProperties\": {\n                \"^\\\\d+$\": true\n            },\n            \"additionalProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft6")]
[TestClass]
public class SuitePatternPropertiesWithNonAsciiDigits
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
    public void TestAsciiDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"42\": \"life, the universe, and everything\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAsciiNonDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"-%#\": \"spending the year dead for tax reasons\" }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"৪২\": \"khajit has wares if you have coin\" }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft6/optional/ecmascript-regex.json",
                "{\n            \"type\": \"object\",\n            \"patternProperties\": {\n                \"^\\\\p{digit}+$\": true\n            },\n            \"additionalProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
