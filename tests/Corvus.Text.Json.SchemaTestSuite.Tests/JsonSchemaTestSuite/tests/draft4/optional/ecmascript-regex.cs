using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex;

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestMatchesInPythonButNotInEcma262()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"abc\\\\n\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"abc\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^abc$\"\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\\t\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0009\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\t$\"\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\\cC\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0003\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\cC$\"\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\\\cc\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0003\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\cc$\"\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiZeroMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNkoDigitZeroDoesNotMatchUnlikeEGPython()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"߀\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNkoDigitZeroAsUEscapeDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u07c0\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\d$\"\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiZeroDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"0\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNkoDigitZeroMatchesUnlikeEGPython()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"߀\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNkoDigitZeroAsUEscapeMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u07c0\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\D$\"\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiAMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLatin1EAcuteDoesNotMatchUnlikeEGPython()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"é\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\w$\"\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiADoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLatin1EAcuteMatchesUnlikeEGPython()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"é\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\W$\"\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiSpaceMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\" \"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestCharacterTabulationMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\t\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLineTabulationMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u000b\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFormFeedMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u000c\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLatin1NonBreakingSpaceMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u00a0\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroWidthWhitespaceMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\ufeff\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLineFeedMatchesLineTerminator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u000a\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestParagraphSeparatorMatchesLineTerminator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u2029\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmSpaceMatchesSpaceSeparator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u2003\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonWhitespaceControlDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0001\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonWhitespaceDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u2013\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\s$\"\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiSpaceDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\" \"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestCharacterTabulationDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\t\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLineTabulationDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u000b\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestFormFeedDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u000c\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLatin1NonBreakingSpaceDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u00a0\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestZeroWidthWhitespaceDoesNotMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\ufeff\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLineFeedDoesNotMatchLineTerminator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u000a\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestParagraphSeparatorDoesNotMatchLineTerminator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u2029\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestEmSpaceDoesNotMatchSpaceSeparator()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u2003\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonWhitespaceControlMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u0001\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonWhitespaceMatches()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"\\u2013\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\S$\"\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiCharacterInJsonString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'\\u00e9cole, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"LES HIVERS DE MON ENFANCE ÉTAIENT DES SAISONS LONGUES, LONGUES. NOUS VIVIONS EN TROIS LIEUX: L'ÉCOLE, L'ÉGLISE ET LA PATINOIRE; MAIS LA VRAIE VIE ÉTAIT SUR LA PATINOIRE.\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{ \"pattern\": \"\\\\p{Letter}cole\" }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiCharacterInJsonString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'\\u00e9cole, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"LES HIVERS DE MON ENFANCE ÉTAIENT DES SAISONS LONGUES, LONGUES. NOUS VIVIONS EN TROIS LIEUX: L'ÉCOLE, L'ÉGLISE ET LA PATINOIRE; MAIS LA VRAIE VIE ÉTAIT SUR LA PATINOIRE.\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{ \"pattern\": \"\\\\wcole\" }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'\\u00e9cole, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAsciiCharactersMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{ \"pattern\": \"[a-z]cole\" }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"42\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAsciiNonDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"-%#\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"৪২\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{ \"pattern\": \"^\\\\d+$\" }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"42\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAsciiNonDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"-%#\"");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"৪২\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{ \"pattern\": \"^\\\\p{digit}+$\" }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiCharacterInJsonString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"l'ecole\": \"pas de vraie vie\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"l'école\": \"pas de vraie vie\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"l'\\u00e9cole\": \"pas de vraie vie\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"L'ÉCOLE\": \"PAS DE VRAIE VIE\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"\\\\p{Letter}cole\": {}\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiCharacterInJsonString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"l'ecole\": \"pas de vraie vie\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"l'école\": \"pas de vraie vie\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"l'\\u00e9cole\": \"pas de vraie vie\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"L'ÉCOLE\": \"PAS DE VRAIE VIE\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"\\\\wcole\": {}\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"l'école\": \"pas de vraie vie\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"l'\\u00e9cole\": \"pas de vraie vie\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAsciiCharactersMatch()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"l'ecole\": \"pas de vraie vie\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"[a-z]cole\": {}\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"42\": \"life, the universe, and everything\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAsciiNonDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"-%#\": \"spending the year dead for tax reasons\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"৪২\": \"khajit has wares if you have coin\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"^\\\\d+$\": {}\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft4")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAsciiDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"42\": \"life, the universe, and everything\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAsciiNonDigits()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"-%#\": \"spending the year dead for tax reasons\" }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"৪২\": \"khajit has wares if you have coin\" }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft4\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"^\\\\p{digit}+$\": {}\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft4.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
