using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex;

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteEcma262RegexDoesNotMatchTrailingNewline : IClassFixture<SuiteEcma262RegexDoesNotMatchTrailingNewline.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEcma262RegexDoesNotMatchTrailingNewline(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchesInPythonButNotInEcma262()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abc\\\\n\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abc\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^abc$\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteEcma262RegexConvertsTToHorizontalTab : IClassFixture<SuiteEcma262RegexConvertsTToHorizontalTab.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEcma262RegexConvertsTToHorizontalTab(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\t\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0009\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\t$\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteEcma262RegexEscapesControlCodesWithCAndUpperLetter : IClassFixture<SuiteEcma262RegexEscapesControlCodesWithCAndUpperLetter.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEcma262RegexEscapesControlCodesWithCAndUpperLetter(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\cC\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0003\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\cC$\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteEcma262RegexEscapesControlCodesWithCAndLowerLetter : IClassFixture<SuiteEcma262RegexEscapesControlCodesWithCAndLowerLetter.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEcma262RegexEscapesControlCodesWithCAndLowerLetter(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\cc\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0003\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\cc$\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteEcma262DMatchesAsciiDigitsOnly : IClassFixture<SuiteEcma262DMatchesAsciiDigitsOnly.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEcma262DMatchesAsciiDigitsOnly(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiZeroMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNkoDigitZeroDoesNotMatchUnlikeEGPython()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"߀\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNkoDigitZeroAsUEscapeDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u07c0\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\d$\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteEcma262DMatchesEverythingButAsciiDigits : IClassFixture<SuiteEcma262DMatchesEverythingButAsciiDigits.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEcma262DMatchesEverythingButAsciiDigits(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiZeroDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNkoDigitZeroMatchesUnlikeEGPython()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"߀\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNkoDigitZeroAsUEscapeMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u07c0\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\D$\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteEcma262WMatchesAsciiLettersOnly : IClassFixture<SuiteEcma262WMatchesAsciiLettersOnly.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEcma262WMatchesAsciiLettersOnly(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiAMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLatin1EAcuteDoesNotMatchUnlikeEGPython()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"é\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\w$\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteEcma262WMatchesEverythingButAsciiLetters : IClassFixture<SuiteEcma262WMatchesEverythingButAsciiLetters.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEcma262WMatchesEverythingButAsciiLetters(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiADoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLatin1EAcuteMatchesUnlikeEGPython()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"é\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\W$\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteEcma262SMatchesWhitespace : IClassFixture<SuiteEcma262SMatchesWhitespace.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEcma262SMatchesWhitespace(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiSpaceMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\" \"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestCharacterTabulationMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\t\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLineTabulationMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u000b\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFormFeedMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u000c\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLatin1NonBreakingSpaceMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u00a0\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroWidthWhitespaceMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\ufeff\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLineFeedMatchesLineTerminator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u000a\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestParagraphSeparatorMatchesLineTerminator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u2029\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmSpaceMatchesSpaceSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u2003\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonWhitespaceControlDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0001\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonWhitespaceDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u2013\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\s$\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteEcma262SMatchesEverythingButWhitespace : IClassFixture<SuiteEcma262SMatchesEverythingButWhitespace.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEcma262SMatchesEverythingButWhitespace(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiSpaceDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\" \"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestCharacterTabulationDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\t\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLineTabulationDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u000b\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFormFeedDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u000c\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLatin1NonBreakingSpaceDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u00a0\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroWidthWhitespaceDoesNotMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\ufeff\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLineFeedDoesNotMatchLineTerminator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u000a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestParagraphSeparatorDoesNotMatchLineTerminator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u2029\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmSpaceDoesNotMatchSpaceSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u2003\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonWhitespaceControlMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0001\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonWhitespaceMatches()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u2013\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\S$\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuitePatternsAlwaysUseUnicodeSemanticsWithPattern : IClassFixture<SuitePatternsAlwaysUseUnicodeSemanticsWithPattern.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternsAlwaysUseUnicodeSemanticsWithPattern(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'\\u00e9cole, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"LES HIVERS DE MON ENFANCE ÉTAIENT DES SAISONS LONGUES, LONGUES. NOUS VIVIONS EN TROIS LIEUX: L'ÉCOLE, L'ÉGLISE ET LA PATINOIRE; MAIS LA VRAIE VIE ÉTAIT SUR LA PATINOIRE.\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{ \"pattern\": \"\\\\p{Letter}cole\" }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteWInPatternsMatchesAZaZ09NotUnicodeLetters : IClassFixture<SuiteWInPatternsMatchesAZaZ09NotUnicodeLetters.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteWInPatternsMatchesAZaZ09NotUnicodeLetters(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'\\u00e9cole, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"LES HIVERS DE MON ENFANCE ÉTAIENT DES SAISONS LONGUES, LONGUES. NOUS VIVIONS EN TROIS LIEUX: L'ÉCOLE, L'ÉGLISE ET LA PATINOIRE; MAIS LA VRAIE VIE ÉTAIT SUR LA PATINOIRE.\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{ \"pattern\": \"\\\\wcole\" }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuitePatternWithAsciiRanges : IClassFixture<SuitePatternWithAsciiRanges.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternWithAsciiRanges(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'\\u00e9cole, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAsciiCharactersMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{ \"pattern\": \"[a-z]cole\" }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteDInPatternMatches09NotUnicodeDigits : IClassFixture<SuiteDInPatternMatches09NotUnicodeDigits.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDInPatternMatches09NotUnicodeDigits(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"42\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAsciiNonDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-%#\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"৪২\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{ \"pattern\": \"^\\\\d+$\" }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuitePatternWithNonAsciiDigits : IClassFixture<SuitePatternWithNonAsciiDigits.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternWithNonAsciiDigits(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"42\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAsciiNonDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-%#\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"৪২\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{ \"pattern\": \"^\\\\p{digit}+$\" }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuitePatternsAlwaysUseUnicodeSemanticsWithPatternProperties : IClassFixture<SuitePatternsAlwaysUseUnicodeSemanticsWithPatternProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternsAlwaysUseUnicodeSemanticsWithPatternProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'ecole\": \"pas de vraie vie\" }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'école\": \"pas de vraie vie\" }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'\\u00e9cole\": \"pas de vraie vie\" }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"L'ÉCOLE\": \"PAS DE VRAIE VIE\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"\\\\p{Letter}cole\": true\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteWInPatternPropertiesMatchesAZaZ09NotUnicodeLetters : IClassFixture<SuiteWInPatternPropertiesMatchesAZaZ09NotUnicodeLetters.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteWInPatternPropertiesMatchesAZaZ09NotUnicodeLetters(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'ecole\": \"pas de vraie vie\" }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'école\": \"pas de vraie vie\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'\\u00e9cole\": \"pas de vraie vie\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"L'ÉCOLE\": \"PAS DE VRAIE VIE\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"\\\\wcole\": true\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuitePatternPropertiesWithAsciiRanges : IClassFixture<SuitePatternPropertiesWithAsciiRanges.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternPropertiesWithAsciiRanges(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'école\": \"pas de vraie vie\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'\\u00e9cole\": \"pas de vraie vie\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAsciiCharactersMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"l'ecole\": \"pas de vraie vie\" }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"[a-z]cole\": true\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteDInPatternPropertiesMatches09NotUnicodeDigits : IClassFixture<SuiteDInPatternPropertiesMatches09NotUnicodeDigits.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDInPatternPropertiesMatches09NotUnicodeDigits(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"42\": \"life, the universe, and everything\" }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAsciiNonDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"-%#\": \"spending the year dead for tax reasons\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"৪২\": \"khajit has wares if you have coin\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"^\\\\d+$\": true\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuitePatternPropertiesWithNonAsciiDigits : IClassFixture<SuitePatternPropertiesWithNonAsciiDigits.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePatternPropertiesWithNonAsciiDigits(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAsciiDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"42\": \"life, the universe, and everything\" }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAsciiNonDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"-%#\": \"spending the year dead for tax reasons\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"৪২\": \"khajit has wares if you have coin\" }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\optional\\ecmascript-regex.json",
                "{\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"^\\\\p{digit}+$\": true\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
