// <copyright file="EcmaRegexTranslatorTranslationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.CodeGenerator.Tests;

using System.Buffers;
using Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Tests that verify the pattern string translation from ECMAScript to .NET syntax.
/// </summary>
public class EcmaRegexTranslatorTranslationTests
{
    // ============================
    // Literal pass-through
    // ============================

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("", "")]
    [InlineData("hello world", "hello world")]
    [InlineData("123", "123")]
    public void LiteralsPassThrough(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Dot expansion
    // ============================

    [Theory]
    [InlineData(".", @"[^\n\r\u2028\u2029]")]
    [InlineData(".+", @"[^\n\r\u2028\u2029]+")]
    [InlineData("a.b", @"a[^\n\r\u2028\u2029]b")]
    [InlineData("...", @"[^\n\r\u2028\u2029][^\n\r\u2028\u2029][^\n\r\u2028\u2029]")]
    public void DotIsExpandedCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Shorthand character classes (standalone)
    // ============================

    [Theory]
    [InlineData(@"\d", "[0-9]")]
    [InlineData(@"\D", "[^0-9]")]
    [InlineData(@"\w", "[a-zA-Z0-9_]")]
    [InlineData(@"\W", "[^a-zA-Z0-9_]")]
    [InlineData(@"\s", @"[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    [InlineData(@"\S", @"[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    public void ShorthandClassesExpandCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Word boundaries
    // ============================

    [Theory]
    [InlineData(@"\b", @"(?:(?<=[a-zA-Z0-9_])(?![a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?=[a-zA-Z0-9_]))")]
    [InlineData(@"\B", @"(?:(?<=[a-zA-Z0-9_])(?=[a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?![a-zA-Z0-9_]))")]
    public void WordBoundariesExpandCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Unicode escapes
    // ============================

    [Theory]
    [InlineData(@"\u0041", @"\u0041")]             // \uXXXX pass-through
    [InlineData(@"\u{41}", @"\u0041")]              // \u{XX} → \u00XX
    [InlineData(@"\u{0041}", @"\u0041")]            // \u{XXXX} → \uXXXX
    [InlineData(@"\u{1F600}", @"(?:\uD83D\uDE00)")]     // Supplementary → surrogate pair in group
    [InlineData(@"\u{10FFFF}", @"(?:\uDBFF\uDFFF)")]    // Max code point in group
    public void UnicodeEscapesTranslateCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Unicode property escapes
    // ============================

    [Theory]
    [InlineData(@"\p{L}", @"\p{L}")]                              // Short name pass-through
    [InlineData(@"\p{Lu}", @"\p{Lu}")]                             // Short name pass-through
    [InlineData(@"\p{Letter}", @"\p{L}")]                          // Long name → short name
    [InlineData(@"\p{Uppercase_Letter}", @"\p{Lu}")]               // Long name → short name
    [InlineData(@"\p{Decimal_Number}", @"\p{Nd}")]                 // Long name → short name
    [InlineData(@"\P{Letter}", @"\P{L}")]                          // Negated long name
    [InlineData(@"\P{Number}", @"\P{N}")]                          // Negated long name
    [InlineData(@"\p{gc=L}", @"\p{L}")]                            // gc= prefix stripped
    [InlineData(@"\p{General_Category=Letter}", @"\p{L}")]         // General_Category= prefix stripped + mapped
    [InlineData(@"\p{Script=Latin}",
        "(?:[\\u0041-\\u005A\\u0061-\\u007A\\u00AA\\u00BA\\u00C0-\\u00D6\\u00D8-\\u00F6"
        + "\\u00F8-\\u02B8\\u02E0-\\u02E4\\u1D00-\\u1D25\\u1D2C-\\u1D5C\\u1D62-\\u1D65"
        + "\\u1D6B-\\u1D77\\u1D79-\\u1DBE\\u1E00-\\u1EFF\\u2071\\u207F\\u2090-\\u209C"
        + "\\u212A-\\u212B\\u2132\\u214E\\u2160-\\u2188\\u2C60-\\u2C7F\\uA722-\\uA787"
        + "\\uA78B-\\uA7CD\\uA7D0-\\uA7D1\\uA7D3\\uA7D5-\\uA7DC\\uA7F2-\\uA7FF"
        + "\\uAB30-\\uAB5A\\uAB5C-\\uAB64\\uAB66-\\uAB69\\uFB00-\\uFB06\\uFF21-\\uFF3A"
        + "\\uFF41-\\uFF5A]|\\uD801[\\uDF80-\\uDF85]|\\uD801[\\uDF87-\\uDFB0]"
        + "|\\uD801[\\uDFB2-\\uDFBA]|\\uD837[\\uDF00-\\uDF1E]|\\uD837[\\uDF25-\\uDF2A])")]
    [InlineData(@"\p{sc=Greek}", @"\p{IsGreek}")]                  // sc= → Is prefix
    [InlineData(@"\p{Script_Extensions=Cyrillic}", @"\p{IsCyrillic}")]  // scx → Is prefix
    [InlineData(@"\p{ASCII}", @"[\u0000-\u007F]")]                 // Binary property → class
    [InlineData(@"\P{ASCII}", @"[^\u0000-\u007F]")]                // Negated binary property
    [InlineData(@"\p{White_Space}",
        @"[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    public void PropertyEscapesTranslateCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Unicode General Category aliases (from PropertyValueAliases.txt)
    // ============================

    [Theory]
    [InlineData(@"\p{digit}", @"\p{Nd}")]                          // digit → Nd (Decimal_Number)
    [InlineData(@"\P{digit}", @"\P{Nd}")]                          // Negated digit
    [InlineData(@"\p{cntrl}", @"\p{Cc}")]                          // cntrl → Cc (Control)
    [InlineData(@"\P{cntrl}", @"\P{Cc}")]                          // Negated cntrl
    [InlineData(@"\p{punct}", @"\p{P}")]                           // punct → P (Punctuation)
    [InlineData(@"\P{punct}", @"\P{P}")]                           // Negated punct
    [InlineData(@"\p{Combining_Mark}", @"\p{M}")]                  // Combining_Mark → M (Mark)
    [InlineData(@"\P{Combining_Mark}", @"\P{M}")]                  // Negated Combining_Mark
    [InlineData(@"^\p{digit}+$", @"^\p{Nd}+$")]                   // digit in context
    [InlineData(@"\p{gc=digit}", @"\p{Nd}")]                       // gc= prefix with alias
    [InlineData(@"\p{General_Category=digit}", @"\p{Nd}")]         // General_Category= prefix with alias
    public void GeneralCategoryAliasesTranslateCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Simple character classes (no negated shorthands)
    // ============================

    [Theory]
    [InlineData("[abc]", "[abc]")]
    [InlineData("[a-z]", "[a-z]")]
    [InlineData("[^abc]", "[^abc]")]
    [InlineData("[^a-z]", "[^a-z]")]
    [InlineData(@"[\d]", "[0-9]")]
    [InlineData(@"[\w]", "[a-zA-Z0-9_]")]
    [InlineData(@"[\s]", @"[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    [InlineData(@"[^\d]", "[^0-9]")]
    [InlineData(@"[^\w]", "[^a-zA-Z0-9_]")]
    [InlineData(@"[\da-f]", "[0-9a-f]")]
    [InlineData(@"[\w.]", "[a-zA-Z0-9_.]")]
    [InlineData(@"[a\-z]", @"[a\-z]")]
    public void SimpleCharacterClassesTranslateCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Character classes with negated shorthands (non-negated class)
    // ============================

    [Theory]
    [InlineData(@"[\D]", "[^0-9]")]
    [InlineData(@"[\W]", "[^a-zA-Z0-9_]")]
    [InlineData(@"[a\D]", "(?:[a]|[^0-9])")]
    [InlineData(@"[a-z\D]", "(?:[a-z]|[^0-9])")]
    [InlineData(@"[\D\W]", "(?:[^0-9]|[^a-zA-Z0-9_])")]
    public void NonNegatedCharClassWithNegShorthandsUsesAlternation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Character classes with negated shorthands (negated class)
    // ============================

    [Theory]
    [InlineData(@"[^\D]", "[0-9]")]
    [InlineData(@"[^\W]", "[a-zA-Z0-9_]")]
    [InlineData(@"[^a\D]", "[0-9-[a]]")]
    [InlineData(@"[^a-f\D]", "[0-9-[a-f]]")]
    [InlineData(@"[^\D\W]", "[0-9]")]  // intersection of [0-9] and [a-zA-Z0-9_] = [0-9]
    public void NegatedCharClassWithNegShorthandsUsesSubtraction(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Empty and match-all character classes
    // ============================

    [Theory]
    [InlineData("[]", @"[^\s\S]")]     // Empty class → matches nothing
    [InlineData("[^]", @"[\s\S]")]      // Match-all class
    public void SpecialCharacterClassesTranslateCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Pass-through escapes
    // ============================

    [Theory]
    [InlineData(@"\t", @"\t")]
    [InlineData(@"\n", @"\n")]
    [InlineData(@"\r", @"\r")]
    [InlineData(@"\f", @"\f")]
    [InlineData(@"\v", @"\v")]
    [InlineData(@"\0", @"\0")]
    [InlineData(@"\\", @"\\")]
    [InlineData(@"\.", @"\.")]
    [InlineData(@"\*", @"\*")]
    [InlineData(@"\+", @"\+")]
    [InlineData(@"\?", @"\?")]
    [InlineData(@"\^", @"\^")]
    [InlineData(@"\$", @"\$")]
    [InlineData(@"\|", @"\|")]
    [InlineData(@"\(", @"\(")]
    [InlineData(@"\)", @"\)")]
    [InlineData(@"\[", @"\[")]
    [InlineData(@"\]", @"\]")]
    [InlineData(@"\{", @"\{")]
    [InlineData(@"\}", @"\}")]
    [InlineData(@"\/", @"\/")]
    public void EscapeSequencesPassThrough(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Hex and control escapes
    // ============================

    [Theory]
    [InlineData(@"\x41", @"\x41")]
    [InlineData(@"\x0A", @"\x0A")]
    [InlineData(@"\cA", @"\cA")]
    [InlineData(@"\cZ", @"\cZ")]
    public void HexAndControlEscapesPassThrough(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Groups
    // ============================

    [Theory]
    [InlineData("(abc)", "(abc)")]
    [InlineData("(?:abc)", "(?:abc)")]
    [InlineData("(?=abc)", "(?=abc)")]
    [InlineData("(?!abc)", "(?!abc)")]
    [InlineData("(?<=abc)", "(?<=abc)")]
    [InlineData("(?<!abc)", "(?<!abc)")]
    [InlineData("(?<name>abc)", "(?<name>abc)")]
    public void GroupsPassThrough(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Backreferences
    // ============================

    [Theory]
    [InlineData(@"(a)\1", @"(a)(?(1)\1)")]
    [InlineData(@"(a)(b)\2", @"(a)(b)(?(2)\2)")]
    [InlineData(@"(?<name>a)\k<name>", @"(?<name>a)(?(name)\k<name>)")]
    public void BackreferencesPassThrough(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Quantifiers
    // ============================

    [Theory]
    [InlineData("a*", "a*")]
    [InlineData("a+", "a+")]
    [InlineData("a?", "a?")]
    [InlineData("a*?", "a*?")]
    [InlineData("a+?", "a+?")]
    [InlineData("a??", "a??")]
    [InlineData("a{3}", "a{3}")]
    [InlineData("a{3,}", "a{3,}")]
    [InlineData("a{3,5}", "a{3,5}")]
    [InlineData("a{3,5}?", "a{3,5}?")]
    public void QuantifiersPassThrough(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Anchors and alternation
    // ============================

    [Theory]
    [InlineData("^abc$", "^abc$")]
    [InlineData("a|b|c", "a|b|c")]
    [InlineData("^(a|b)$", "^(a|b)$")]
    public void AnchorsAndAlternationPassThrough(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Complex patterns
    // ============================

    [Fact]
    public void DatePatternTranslatesCorrectly()
    {
        // ECMAScript: ^\d{4}-\d{2}-\d{2}$
        string ecma = @"^\d{4}-\d{2}-\d{2}$";
        string result = EcmaRegexTranslator.Translate(ecma);
        Assert.Equal("^[0-9]{4}-[0-9]{2}-[0-9]{2}$", result);
    }

    [Fact]
    public void WordBoundaryPatternTranslatesCorrectly()
    {
        // \bfoo\b
        string ecma = @"\bfoo\b";
        string result = EcmaRegexTranslator.Translate(ecma);
        string wb = @"(?:(?<=[a-zA-Z0-9_])(?![a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?=[a-zA-Z0-9_]))";
        Assert.Equal($"{wb}foo{wb}", result);
    }

    [Fact]
    public void EmailLikePatternTranslatesCorrectly()
    {
        // [\w.+-]+@[\w-]+\.[\w.]+
        string ecma = @"[\w.+-]+@[\w-]+\.[\w.]+";
        string result = EcmaRegexTranslator.Translate(ecma);
        Assert.Equal(@"[a-zA-Z0-9_.+-]+@[a-zA-Z0-9_-]+\.[a-zA-Z0-9_.]+", result);
    }

    // ============================
    // TryTranslate span API
    // ============================

    [Fact]
    public void TryTranslateSucceedsWithAdequateBuffer()
    {
        ReadOnlySpan<char> ecma = @"\d+";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out int written);

        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal("[0-9]+", new string(buffer[..written]));
    }

    [Fact]
    public void TryTranslateReturnsDestinationTooSmallForTinyBuffer()
    {
        ReadOnlySpan<char> ecma = @"\d+";
        Span<char> buffer = stackalloc char[2];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.Equal(OperationStatus.DestinationTooSmall, status);
    }

    [Fact]
    public void TryTranslateReturnsInvalidDataForBadEscape()
    {
        // Lone backslash at end of pattern
        ReadOnlySpan<char> ecma = @"abc\";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.Equal(OperationStatus.InvalidData, status);
    }

    [Fact]
    public void GetMaxTranslatedLengthReturnsAdequateSize()
    {
        string ecma = @"\b\w+\b";
        int maxLen = EcmaRegexTranslator.GetMaxTranslatedLength(ecma);
        string result = EcmaRegexTranslator.Translate(ecma);

        Assert.True(maxLen >= result.Length, $"MaxLen {maxLen} < actual {result.Length}");
    }

    // ============================
    // Character class with escapes inside
    // ============================

    [Theory]
    [InlineData(@"[\b]", @"[\b]")]         // \b = backspace inside char class
    [InlineData(@"[\x41]", @"[\x41]")]     // hex escape inside class
    [InlineData(@"[\u0041]", @"[\u0041]")] // unicode escape inside class
    [InlineData(@"[\u{41}]", @"[\u0041]")] // brace unicode escape inside class
    public void EscapesInsideCharClassTranslateCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Unicode property escapes inside character classes
    // ============================

    [Theory]
    [InlineData(@"[\p{L}]", @"[\p{L}]")]
    [InlineData(@"[\p{Letter}]", @"[\p{L}]")]
    [InlineData(@"[^\p{L}]", @"[^\p{L}]")]
    public void PropertyEscapesInsideCharClassTranslateCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // All General Category long name mappings
    // ============================

    [Theory]
    [InlineData("Letter", "L")]
    [InlineData("Cased_Letter", "LC")]
    [InlineData("Uppercase_Letter", "Lu")]
    [InlineData("Lowercase_Letter", "Ll")]
    [InlineData("Titlecase_Letter", "Lt")]
    [InlineData("Modifier_Letter", "Lm")]
    [InlineData("Other_Letter", "Lo")]
    [InlineData("Mark", "M")]
    [InlineData("Nonspacing_Mark", "Mn")]
    [InlineData("Spacing_Mark", "Mc")]
    [InlineData("Enclosing_Mark", "Me")]
    [InlineData("Number", "N")]
    [InlineData("Decimal_Number", "Nd")]
    [InlineData("Letter_Number", "Nl")]
    [InlineData("Other_Number", "No")]
    [InlineData("Punctuation", "P")]
    [InlineData("Connector_Punctuation", "Pc")]
    [InlineData("Dash_Punctuation", "Pd")]
    [InlineData("Open_Punctuation", "Ps")]
    [InlineData("Close_Punctuation", "Pe")]
    [InlineData("Initial_Punctuation", "Pi")]
    [InlineData("Final_Punctuation", "Pf")]
    [InlineData("Other_Punctuation", "Po")]
    [InlineData("Symbol", "S")]
    [InlineData("Math_Symbol", "Sm")]
    [InlineData("Currency_Symbol", "Sc")]
    [InlineData("Modifier_Symbol", "Sk")]
    [InlineData("Other_Symbol", "So")]
    [InlineData("Separator", "Z")]
    [InlineData("Space_Separator", "Zs")]
    [InlineData("Line_Separator", "Zl")]
    [InlineData("Paragraph_Separator", "Zp")]
    [InlineData("Other", "C")]
    [InlineData("Control", "Cc")]
    [InlineData("Format", "Cf")]
    [InlineData("Surrogate", "Cs")]
    [InlineData("Private_Use", "Co")]
    [InlineData("Unassigned", "Cn")]
    public void AllCategoryLongNamesMapCorrectly(string longName, string expectedShort)
    {
        string ecma = $@"\p{{{longName}}}";
        string expected = $@"\p{{{expectedShort}}}";
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Literal non-BMP characters (surrogate pairs in UTF-16)
    // ============================

    [Theory]
    [InlineData("^\U0001F432*$", @"^(?:\uD83D\uDC32)*$")]                // 🐲* — quantifier on whole code point
    [InlineData("\U0001F432", @"(?:\uD83D\uDC32)")]                       // bare literal
    [InlineData("\U0001F432+", @"(?:\uD83D\uDC32)+")]                     // quantifier +
    [InlineData("\U0001F432?", @"(?:\uD83D\uDC32)?")]                     // quantifier ?
    [InlineData("\U0001F432{2}", @"(?:\uD83D\uDC32){2}")]                 // quantifier {n}
    [InlineData("\U0001F432\U0001F409", @"(?:\uD83D\uDC32)(?:\uD83D\uDC09)")] // two literals
    [InlineData("a\U0001F432b", @"a(?:\uD83D\uDC32)b")]                  // mixed with ASCII
    public void LiteralNonBmpCharactersTranslateCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // \u{XXXXX} escapes wrap supplementary in non-capturing group
    // ============================

    [Theory]
    [InlineData(@"\u{1F432}", @"(?:\uD83D\uDC32)")]
    [InlineData(@"\u{1F432}*", @"(?:\uD83D\uDC32)*")]
    [InlineData(@"\u{1F432}+", @"(?:\uD83D\uDC32)+")]
    [InlineData(@"\u{41}", @"\u0041")]                  // BMP code point — no group needed
    public void UnicodeEscapeBraceFormWrapsSupplementaryCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Non-BMP inside character classes
    // ============================

    [Fact]
    public void NonBmpLiteralInCharClassProducesAlternation()
    {
        // [\U0001F432] → alternation with surrogate pair
        string result = EcmaRegexTranslator.Translate("[\U0001F432]");
        Assert.Contains(@"\uD83D", result);
        Assert.Contains(@"\uDC32", result);
    }

    [Fact]
    public void NonBmpLiteralWithBmpInCharClassProducesAlternation()
    {
        // [a\U0001F432z] → (?:[az]|\uD83D\uDC32)
        string result = EcmaRegexTranslator.Translate("[a\U0001F432z]");
        Assert.Contains(@"\uD83D", result);
        Assert.Contains(@"\uDC32", result);
        // Should also contain the BMP chars
        Assert.Contains("a", result);
        Assert.Contains("z", result);
    }
}
