// <copyright file="EcmaRegexTranslatorMatchingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGenerator.Tests;

using System.Text.RegularExpressions;

/// <summary>
/// End-to-end tests that translate an ECMAScript pattern and verify the resulting
/// .NET <see cref="Regex"/> matches (or doesn't match) specific input strings.
/// These validate that the translated pattern has correct runtime semantics.
/// </summary>
public class EcmaRegexTranslatorMatchingTests
{
    // ============================
    // \d — ASCII digits only
    // ============================

    [Theory]
    [InlineData(@"\d", "0", true)]
    [InlineData(@"\d", "5", true)]
    [InlineData(@"\d", "9", true)]
    [InlineData(@"\d", "a", false)]
    [InlineData(@"\d", "Z", false)]
    [InlineData(@"\d", "\u0660", false)]  // Arabic-Indic Digit Zero — NOT matched by ECMAScript \d
    [InlineData(@"\d", "\u0967", false)]  // Devanagari Digit One
    [InlineData(@"\d", "\u00B2", false)]  // Superscript Two (No category, not Nd but still a "digit-like")
    public void DigitMatchesOnlyAsciiDigits(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // \D — complement of ASCII digits
    // ============================

    [Theory]
    [InlineData(@"\D", "a", true)]
    [InlineData(@"\D", "Z", true)]
    [InlineData(@"\D", " ", true)]
    [InlineData(@"\D", "\u0660", true)]   // Arabic-Indic Digit Zero — IS matched by ECMAScript \D
    [InlineData(@"\D", "0", false)]
    [InlineData(@"\D", "9", false)]
    public void NotDigitMatchesNonAsciiDigits(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // \w — ASCII word characters only
    // ============================

    [Theory]
    [InlineData(@"\w", "a", true)]
    [InlineData(@"\w", "Z", true)]
    [InlineData(@"\w", "0", true)]
    [InlineData(@"\w", "_", true)]
    [InlineData(@"\w", " ", false)]
    [InlineData(@"\w", "-", false)]
    [InlineData(@"\w", "\u00E9", false)]  // é — NOT matched by ECMAScript \w
    [InlineData(@"\w", "\u00FC", false)]  // ü
    [InlineData(@"\w", "\u0410", false)]  // Cyrillic А
    public void WordMatchesOnlyAsciiWordChars(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // \W — complement of ASCII word characters
    // ============================

    [Theory]
    [InlineData(@"\W", " ", true)]
    [InlineData(@"\W", "-", true)]
    [InlineData(@"\W", "\u00E9", true)]   // é IS matched by ECMAScript \W
    [InlineData(@"\W", "\u0410", true)]   // Cyrillic А
    [InlineData(@"\W", "a", false)]
    [InlineData(@"\W", "0", false)]
    [InlineData(@"\W", "_", false)]
    public void NotWordMatchesNonAsciiWordChars(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // \s — ECMAScript whitespace (includes \uFEFF, excludes \x85)
    // ============================

    [Theory]
    [InlineData(@"\s", "\t", true)]        // TAB
    [InlineData(@"\s", "\n", true)]        // LF
    [InlineData(@"\s", "\r", true)]        // CR
    [InlineData(@"\s", "\f", true)]        // FF
    [InlineData(@"\s", "\v", true)]        // VT
    [InlineData(@"\s", " ", true)]         // Space
    [InlineData(@"\s", "\u00A0", true)]    // NBSP
    [InlineData(@"\s", "\uFEFF", true)]    // BOM/ZWNBSP — included in ECMAScript \s
    [InlineData(@"\s", "\u2028", true)]    // Line Separator
    [InlineData(@"\s", "\u2029", true)]    // Paragraph Separator
    [InlineData(@"\s", "\u2003", true)]    // Em Space (Zs category)
    [InlineData(@"\s", "\u3000", true)]    // Ideographic Space
    [InlineData(@"\s", "\u0085", false)]   // NEL — NOT included in ECMAScript \s
    [InlineData(@"\s", "a", false)]
    [InlineData(@"\s", "0", false)]
    public void WhitespaceMatchesEcmaScriptDefinition(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // \S — complement of ECMAScript whitespace
    // ============================

    [Theory]
    [InlineData(@"\S", "a", true)]
    [InlineData(@"\S", "0", true)]
    [InlineData(@"\S", "\u0085", true)]    // NEL IS matched by ECMAScript \S
    [InlineData(@"\S", " ", false)]
    [InlineData(@"\S", "\uFEFF", false)]
    [InlineData(@"\S", "\t", false)]
    public void NotWhitespaceMatchesEcmaScriptDefinition(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // . (dot) — excludes \n, \r, \u2028, \u2029
    // ============================

    [Theory]
    [InlineData(".", "a", true)]
    [InlineData(".", " ", true)]
    [InlineData(".", "0", true)]
    [InlineData(".", "\t", true)]
    [InlineData(".", "\n", false)]
    [InlineData(".", "\r", false)]
    [InlineData(".", "\u2028", false)]     // Line Separator
    [InlineData(".", "\u2029", false)]     // Paragraph Separator
    public void DotExcludesAllLineTerminators(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // \b — word boundary (ASCII definition)
    // ============================

    [Theory]
    [InlineData(@"\bfoo\b", "foo", true)]
    [InlineData(@"\bfoo\b", " foo ", true)]
    [InlineData(@"\bfoo\b", "foobar", false)]
    [InlineData(@"\bfoo\b", "barfoo", false)]
    [InlineData(@"\b\d+\b", "123", true)]
    [InlineData(@"\b\d+\b", " 42 ", true)]
    public void WordBoundaryUsesAsciiDefinition(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // \B — non-word boundary (ASCII definition)
    // ============================

    [Theory]
    [InlineData(@"a\Bb", "ab", true)]
    [InlineData(@"a\Bb", "a b", false)]
    public void NonWordBoundaryUsesAsciiDefinition(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Unicode escape matching
    // ============================

    [Theory]
    [InlineData(@"\u0041", "A", true)]
    [InlineData(@"\u0041", "B", false)]
    [InlineData(@"\u{41}", "A", true)]
    [InlineData(@"\u{41}", "B", false)]
    [InlineData(@"\u{1F600}", "\U0001F600", true)]   // 😀 via surrogate pair
    [InlineData(@"\u{1F600}", "A", false)]
    public void UnicodeEscapesMatchCorrectCharacters(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Unicode property escapes matching
    // ============================

    [Theory]
    [InlineData(@"\p{L}", "a", true)]
    [InlineData(@"\p{L}", "\u0410", true)]    // Cyrillic А
    [InlineData(@"\p{L}", "0", false)]
    [InlineData(@"\p{Lu}", "A", true)]
    [InlineData(@"\p{Lu}", "a", false)]
    [InlineData(@"\p{Nd}", "0", true)]
    [InlineData(@"\p{Nd}", "\u0660", true)]   // Arabic-Indic digit
    [InlineData(@"\p{Nd}", "a", false)]
    [InlineData(@"\P{L}", "0", true)]
    [InlineData(@"\P{L}", "a", false)]
    public void PropertyEscapesMatchCorrectCharacters(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Character classes with \d inside — only ASCII digits
    // ============================

    [Theory]
    [InlineData(@"[\da-f]", "5", true)]
    [InlineData(@"[\da-f]", "c", true)]
    [InlineData(@"[\da-f]", "g", false)]
    [InlineData(@"[\da-f]", "\u0660", false)]  // Arabic digit NOT matched
    public void CharClassDigitMatchesOnlyAscii(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Character classes with \w inside — only ASCII word chars
    // ============================

    [Theory]
    [InlineData(@"[\w-]", "a", true)]
    [InlineData(@"[\w-]", "-", true)]
    [InlineData(@"[\w-]", "\u00E9", false)]  // é NOT matched
    public void CharClassWordMatchesOnlyAscii(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Empty and match-all character classes
    // ============================

    [Theory]
    [InlineData("[^]", "a", true)]
    [InlineData("[^]", "\n", true)]
    [InlineData("[^]", "\r", true)]
    [InlineData("[^]", "\u2028", true)]
    public void MatchAllClassMatchesAnything(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Fact]
    public void EmptyClassMatchesNothing()
    {
        string dotnet = EcmaRegexTranslator.Translate("[]");
        Assert.DoesNotMatch(dotnet, "a");
        Assert.DoesNotMatch(dotnet, " ");
    }

    // ============================
    // Character classes with negated shorthands — matching
    // ============================

    [Theory]
    [InlineData(@"[\D]", "a", true)]
    [InlineData(@"[\D]", " ", true)]
    [InlineData(@"[\D]", "0", false)]
    [InlineData(@"[\D]", "9", false)]
    [InlineData(@"[^\D]", "0", true)]       // [^\D] = [\d] = [0-9]
    [InlineData(@"[^\D]", "5", true)]
    [InlineData(@"[^\D]", "a", false)]
    [InlineData(@"[^\D]", "\u0660", false)] // Arabic digit NOT in ASCII [0-9]
    public void NegatedShorthandCharClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Complex pattern matching
    // ============================

    [Theory]
    [InlineData(@"^\d{4}-\d{2}-\d{2}$", "2024-01-15", true)]
    [InlineData(@"^\d{4}-\d{2}-\d{2}$", "24-1-5", false)]
    [InlineData(@"^\d{4}-\d{2}-\d{2}$", "abcd-ef-gh", false)]
    public void DatePatternMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[\w.+-]+@[\w-]+\.[\w.]+", "user@example.com", true)]
    [InlineData(@"[\w.+-]+@[\w-]+\.[\w.]+", "a+b@c-d.co.uk", true)]
    [InlineData(@"[\w.+-]+@[\w-]+\.[\w.]+", "@missing.com", false)]
    public void EmailPatternMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"^(?:https?|ftp):\/\/", "https://example.com", true)]
    [InlineData(@"^(?:https?|ftp):\/\/", "ftp://files.org", true)]
    [InlineData(@"^(?:https?|ftp):\/\/", "file://local", false)]
    public void UrlSchemePatternMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"([""']).*?\1", "\"hello\"", true)]
    [InlineData(@"([""']).*?\1", "'hello'", true)]
    [InlineData(@"([""']).*?\1", "\"hello'", false)]
    public void QuotedStringPatternMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Quantifiers with expanded classes
    // ============================

    [Theory]
    [InlineData(@"\d+", "123", true)]
    [InlineData(@"\d+", "", false)]
    [InlineData(@"\w{3,5}", "abc", true)]
    [InlineData(@"\w{3,5}", "ab", false)]
    [InlineData(@"\w{3,5}", "abcdef", true)]   // Partial match
    [InlineData(@"^\w{3,5}$", "abcdef", false)] // Exact match fails
    public void QuantifiersWorkWithExpandedClasses(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Named groups and backreferences
    // ============================

    [Theory]
    [InlineData(@"(?<word>\w+)\s+\k<word>", "hello hello", true)]
    [InlineData(@"(?<word>\w+)\s+\k<word>", "hello world", false)]
    public void NamedGroupsAndBackrefsWorkCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Lookahead and lookbehind
    // ============================

    [Theory]
    [InlineData(@"\d+(?= dollars)", "100 dollars", true)]
    [InlineData(@"\d+(?= dollars)", "100 euros", false)]
    [InlineData(@"(?<=\$)\d+", "$100", true)]
    [InlineData(@"(?<=\$)\d+", "100", false)]
    [InlineData(@"\d+(?!\d)", "123a", true)]
    [InlineData(@"(?<!\d)\d+", "a456", true)]
    public void LookaheadAndLookbehindWorkCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Alternation
    // ============================

    [Theory]
    [InlineData(@"cat|dog|fish", "I have a cat", true)]
    [InlineData(@"cat|dog|fish", "I have a dog", true)]
    [InlineData(@"cat|dog|fish", "I have a bird", false)]
    public void AlternationMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Anchors
    // ============================

    [Theory]
    [InlineData(@"^hello", "hello world", true)]
    [InlineData(@"^hello", "say hello", false)]
    [InlineData(@"world$", "hello world", true)]
    [InlineData(@"world$", "world hello", false)]
    [InlineData(@"^exact$", "exact", true)]
    [InlineData(@"^exact$", "not exact", false)]
    public void AnchorsMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Property escapes — long names in matching context
    // ============================

    [Theory]
    [InlineData(@"\p{Letter}", "a", true)]
    [InlineData(@"\p{Letter}", "0", false)]
    [InlineData(@"\p{Uppercase_Letter}", "A", true)]
    [InlineData(@"\p{Uppercase_Letter}", "a", false)]
    [InlineData(@"\p{Decimal_Number}", "5", true)]
    [InlineData(@"\p{Decimal_Number}", "x", false)]
    [InlineData(@"\p{gc=Lu}", "A", true)]
    [InlineData(@"\p{gc=Lu}", "a", false)]
    [InlineData(@"\p{General_Category=Number}", "7", true)]
    [InlineData(@"\p{General_Category=Number}", "z", false)]
    public void PropertyEscapeLongNamesMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Hex and control escapes — matching
    // ============================

    [Theory]
    [InlineData(@"\x41", "A", true)]
    [InlineData(@"\x41", "B", false)]
    [InlineData(@"\x0A", "\n", true)]
    [InlineData(@"\cA", "\u0001", true)]   // Control-A = SOH
    [InlineData(@"\cM", "\r", true)]       // Control-M = CR
    public void HexAndControlEscapesMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Escaped special characters — matching
    // ============================

    [Theory]
    [InlineData(@"\.", ".", true)]
    [InlineData(@"\.", "a", false)]
    [InlineData(@"\*", "*", true)]
    [InlineData(@"\+", "+", true)]
    [InlineData(@"\?", "?", true)]
    [InlineData(@"\(", "(", true)]
    [InlineData(@"\)", ")", true)]
    [InlineData(@"\[", "[", true)]
    [InlineData(@"\]", "]", true)]
    [InlineData(@"\{", "{", true)]
    [InlineData(@"\}", "}", true)]
    [InlineData(@"\|", "|", true)]
    [InlineData(@"\\", "\\", true)]
    [InlineData(@"\/", "/", true)]
    [InlineData(@"\^", "^", true)]
    [InlineData(@"\$", "$", true)]
    public void EscapedSpecialCharactersMatchLiterally(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Mixed patterns — realistic use cases
    // ============================

    [Theory]
    [InlineData(@"^\s*$", "   ", true)]
    [InlineData(@"^\s*$", "", true)]
    [InlineData(@"^\s*$", "  a  ", false)]
    public void WhitespaceOnlyPattern(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"^#[\da-fA-F]{6}$", "#FF00AA", true)]
    [InlineData(@"^#[\da-fA-F]{6}$", "#ff00aa", true)]
    [InlineData(@"^#[\da-fA-F]{6}$", "#GGHHII", false)]
    [InlineData(@"^#[\da-fA-F]{6}$", "FF00AA", false)]
    public void HexColorPattern(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"^-?\d+(\.\d+)?$", "42", true)]
    [InlineData(@"^-?\d+(\.\d+)?$", "-3.14", true)]
    [InlineData(@"^-?\d+(\.\d+)?$", "0.5", true)]
    [InlineData(@"^-?\d+(\.\d+)?$", "abc", false)]
    [InlineData(@"^-?\d+(\.\d+)?$", "1.", false)]
    public void NumberPattern(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"^(?=.*\d)(?=.*[a-z])(?=.*[A-Z]).{8,}$", "Passw0rd", true)]
    [InlineData(@"^(?=.*\d)(?=.*[a-z])(?=.*[A-Z]).{8,}$", "password", false)]
    [InlineData(@"^(?=.*\d)(?=.*[a-z])(?=.*[A-Z]).{8,}$", "Short1", false)]
    public void PasswordStrengthPattern(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Interaction: dot inside character class is literal
    // ============================

    [Theory]
    [InlineData(@"[.]", ".", true)]
    [InlineData(@"[.]", "a", false)]   // dot is literal inside char class
    public void DotInsideCharClassIsLiteral(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Interaction: \0 (null character)
    // ============================

    [Theory]
    [InlineData(@"\0", "\0", true)]
    [InlineData(@"\0", "a", false)]
    public void NullEscapeMatchesNullCharacter(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Regression: patterns that combine multiple translated features
    // ============================

    [Theory]
    [InlineData(@"\b\w+@\w+\.\w+\b", "me user@host.com end", true)]
    [InlineData(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", "IP is 192.168.1.1 here", true)]
    [InlineData(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", "not an ip", false)]
    public void CombinedFeaturePatterns(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // General Category aliases (digit, cntrl, punct, Combining_Mark)
    // ============================

    [Theory]
    [InlineData(@"^\p{digit}+$", "42", true)]                     // ASCII digits match
    [InlineData(@"^\p{digit}+$", "\u09EA\u09E8", true)]           // Bengali digits match (U+09EA = ৪, U+09E8 = ২)
    [InlineData(@"^\p{digit}+$", "-%#", false)]                   // Non-digits don't match
    [InlineData(@"^\p{cntrl}$", "\u0000", true)]                  // NULL is a control char
    [InlineData(@"^\p{cntrl}$", "\u001F", true)]                  // Unit separator is control
    [InlineData(@"^\p{cntrl}$", "a", false)]                      // Letter is not control
    [InlineData(@"^\p{punct}+$", ".,;:!?", true)]                 // ASCII punctuation
    [InlineData(@"^\p{punct}+$", "abc", false)]                   // Letters are not punctuation
    [InlineData(@"^\p{Combining_Mark}+$", "\u0300\u0301", true)]  // Combining grave + acute
    [InlineData(@"^\p{Combining_Mark}+$", "a", false)]            // Letter is not a combining mark
    public void GeneralCategoryAliasesMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ============================
    // Literal non-BMP characters
    // ============================

    [Theory]
    [InlineData("^\U0001F432*$", "", true)]                         // matches empty
    [InlineData("^\U0001F432*$", "\U0001F432", true)]               // matches single 🐲
    [InlineData("^\U0001F432*$", "\U0001F432\U0001F432", true)]     // matches two 🐲🐲
    [InlineData("^\U0001F432*$", "\U0001F409", false)]              // doesn't match 🐉
    [InlineData("^\U0001F432*$", "D", false)]                       // doesn't match ASCII
    [InlineData("^\U0001F432+$", "", false)]                        // + requires at least one
    [InlineData("^\U0001F432+$", "\U0001F432", true)]               // + matches one
    [InlineData("^\U0001F432{2}$", "\U0001F432\U0001F432", true)]   // {2} matches exactly two
    [InlineData("^\U0001F432{2}$", "\U0001F432", false)]            // {2} doesn't match one
    public void LiteralNonBmpWithQuantifiersMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"^\u{1F432}*$", "", true)]
    [InlineData(@"^\u{1F432}*$", "\U0001F432", true)]
    [InlineData(@"^\u{1F432}*$", "\U0001F432\U0001F432", true)]
    [InlineData(@"^\u{1F432}*$", "\U0001F409", false)]
    [InlineData(@"^\u{1F432}*$", "D", false)]
    [InlineData(@"^\u{1F432}+$", "\U0001F432", true)]
    [InlineData(@"^\u{1F432}+$", "", false)]
    public void UnicodeEscapeBraceFormMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData("[\U0001F432]", "\U0001F432", true)]
    [InlineData("[\U0001F432]", "\U0001F409", false)]
    [InlineData("[\U0001F432]", "D", false)]
    [InlineData("[a\U0001F432z]", "\U0001F432", true)]
    [InlineData("[a\U0001F432z]", "a", true)]
    [InlineData("[a\U0001F432z]", "z", true)]
    [InlineData("[a\U0001F432z]", "b", false)]
    public void NonBmpInCharClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    private static void AssertMatch(string ecmaPattern, string input, bool shouldMatch)
    {
        string dotNetPattern = EcmaRegexTranslator.Translate(ecmaPattern);
        Regex regex = new(dotNetPattern);
        bool isMatch = regex.IsMatch(input);
        Assert.Equal(shouldMatch, isMatch);
    }
}
