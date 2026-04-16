using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex;

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"abc\\\\n\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"abc\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^abc$\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\\\t\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0009\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\t$\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\\\cC\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0003\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\cC$\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\\\cc\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0003\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\cc$\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"0\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNkoDigitZeroDoesNotMatchUnlikeEGPython()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"߀\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNkoDigitZeroAsUEscapeDoesNotMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u07c0\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\d$\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"0\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNkoDigitZeroMatchesUnlikeEGPython()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"߀\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNkoDigitZeroAsUEscapeMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u07c0\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\D$\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLatin1EAcuteDoesNotMatchUnlikeEGPython()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"é\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\w$\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLatin1EAcuteMatchesUnlikeEGPython()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"é\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\W$\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\" \"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestCharacterTabulationMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\t\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLineTabulationMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u000b\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFormFeedMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u000c\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLatin1NonBreakingSpaceMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u00a0\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroWidthWhitespaceMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\ufeff\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLineFeedMatchesLineTerminator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u000a\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestParagraphSeparatorMatchesLineTerminator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u2029\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmSpaceMatchesSpaceSeparator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u2003\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonWhitespaceControlDoesNotMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0001\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonWhitespaceDoesNotMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u2013\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\s$\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\" \"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestCharacterTabulationDoesNotMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\t\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLineTabulationDoesNotMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u000b\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFormFeedDoesNotMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u000c\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLatin1NonBreakingSpaceDoesNotMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u00a0\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroWidthWhitespaceDoesNotMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\ufeff\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLineFeedDoesNotMatchLineTerminator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u000a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestParagraphSeparatorDoesNotMatchLineTerminator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u2029\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmSpaceDoesNotMatchSpaceSeparator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u2003\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonWhitespaceControlMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0001\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonWhitespaceMatches()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u2013\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"string\",\r\n            \"pattern\": \"^\\\\S$\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'\\u00e9cole, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"LES HIVERS DE MON ENFANCE ÉTAIENT DES SAISONS LONGUES, LONGUES. NOUS VIVIONS EN TROIS LIEUX: L'ÉCOLE, L'ÉGLISE ET LA PATINOIRE; MAIS LA VRAIE VIE ÉTAIT SUR LA PATINOIRE.\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"pattern\": \"\\\\p{Letter}cole\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'\\u00e9cole, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"LES HIVERS DE MON ENFANCE ÉTAIENT DES SAISONS LONGUES, LONGUES. NOUS VIVIONS EN TROIS LIEUX: L'ÉCOLE, L'ÉGLISE ET LA PATINOIRE; MAIS LA VRAIE VIE ÉTAIT SUR LA PATINOIRE.\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"pattern\": \"\\\\wcole\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'école, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance étaient des saisons longues, longues. Nous vivions en trois lieux: l'\\u00e9cole, l'église et la patinoire; mais la vraie vie était sur la patinoire.\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAsciiCharactersMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"Les hivers de mon enfance etaient des saisons longues, longues. Nous vivions en trois lieux: l'ecole, l'eglise et la patinoire; mais la vraie vie etait sur la patinoire.\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"pattern\": \"[a-z]cole\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"42\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAsciiNonDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"-%#\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"৪২\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"pattern\": \"^\\\\d+$\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"42\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAsciiNonDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"-%#\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"৪২\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"pattern\": \"^\\\\p{digit}+$\"\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"l'ecole\": \"pas de vraie vie\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"l'école\": \"pas de vraie vie\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"l'\\u00e9cole\": \"pas de vraie vie\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"L'ÉCOLE\": \"PAS DE VRAIE VIE\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"\\\\p{Letter}cole\": true\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"l'ecole\": \"pas de vraie vie\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLiteralUnicodeCharacterInJsonString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"l'école\": \"pas de vraie vie\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"l'\\u00e9cole\": \"pas de vraie vie\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUnicodeMatchingIsCaseSensitive()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"L'ÉCOLE\": \"PAS DE VRAIE VIE\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"\\\\wcole\": true\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"l'école\": \"pas de vraie vie\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUnicodeCharacterInHexFormatInString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"l'\\u00e9cole\": \"pas de vraie vie\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAsciiCharactersMatch()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"l'ecole\": \"pas de vraie vie\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"[a-z]cole\": true\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"42\": \"life, the universe, and everything\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAsciiNonDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"-%#\": \"spending the year dead for tax reasons\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"৪২\": \"khajit has wares if you have coin\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"^\\\\d+$\": true\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft202012")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"42\": \"life, the universe, and everything\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAsciiNonDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"-%#\": \"spending the year dead for tax reasons\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonAsciiDigitsBengaliDigitFourBengaliDigitTwo()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"৪২\": \"khajit has wares if you have coin\" }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\optional\\ecmascript-regex.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"^\\\\p{digit}+$\": true\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.Optional.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
