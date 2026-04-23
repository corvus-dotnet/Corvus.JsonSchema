// <copyright file="EcmaRegexTranslatorComprehensiveTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.CodeGenerator.Tests;

using System.Buffers;
using Corvus.Text.Json.CodeGeneration;
using System.Text.RegularExpressions;

/// <summary>
/// Comprehensive tests covering all ECMAScript 262 regex grammar productions
/// with /u Unicode mode. Every matching assertion is validated against a real
/// ECMAScript engine (Node.js with /u flag) before inclusion.
/// </summary>
public class EcmaRegexTranslatorComprehensiveTests
{
    // ================================================================
    // 1. ALL WHITESPACE CHARACTERS — individual Zs category members
    // ================================================================

    [Theory]
    [InlineData(@"\s", "\u1680", true)]   // Ogham Space Mark
    [InlineData(@"\s", "\u2000", true)]   // En Quad
    [InlineData(@"\s", "\u2001", true)]   // Em Quad
    [InlineData(@"\s", "\u2002", true)]   // En Space
    [InlineData(@"\s", "\u2004", true)]   // Three-Per-Em Space
    [InlineData(@"\s", "\u2005", true)]   // Four-Per-Em Space
    [InlineData(@"\s", "\u2006", true)]   // Six-Per-Em Space
    [InlineData(@"\s", "\u2007", true)]   // Figure Space
    [InlineData(@"\s", "\u2008", true)]   // Punctuation Space
    [InlineData(@"\s", "\u2009", true)]   // Thin Space
    [InlineData(@"\s", "\u200A", true)]   // Hair Space
    [InlineData(@"\s", "\u202F", true)]   // Narrow No-Break Space
    [InlineData(@"\s", "\u205F", true)]   // Medium Mathematical Space
    public void AllZsCategoryMembersMatchWhitespace(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 2. WORD BOUNDARY (\b) — edge cases
    // ================================================================

    [Theory]
    [InlineData(@"\ba", "abc", true)]      // \b at string start before word char
    [InlineData(@"c\b", "abc", true)]      // \b at string end after word char
    [InlineData(@"\b", "a", true)]         // \b matches at start of single word char
    [InlineData(@"\b", " ", false)]        // no word chars → no boundary
    [InlineData(@"^\b", "abc", true)]      // \b at start of string
    [InlineData(@"\b$", "abc", true)]      // \b at end of string
    public void WordBoundaryEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"\bé", "café", true)]     // \b between 'f'(word) and 'é'(non-word) — JS verified
    public void WordBoundaryWithUnicodeChars(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 3. NON-WORD BOUNDARY (\B) — edge cases
    // ================================================================

    [Theory]
    [InlineData(@"\Ba", "ba", true)]       // \B between two word chars
    [InlineData(@"\B.", " a", true)]       // \B between two non-word chars (space + before a, at pos 0)
    public void NonWordBoundaryEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 4. CHARACTER CLASSES WITH \S NEGATED SHORTHAND
    // ================================================================

    [Theory]
    [InlineData(@"[\S]", "a", true)]
    [InlineData(@"[\S]", "0", true)]
    [InlineData(@"[\S]", " ", false)]
    [InlineData(@"[\S]", "\t", false)]
    public void CharClassWithNegatedS(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[^\S]", " ", true)]
    [InlineData(@"[^\S]", "\t", true)]
    [InlineData(@"[^\S]", "a", false)]
    public void NegatedCharClassWithNegatedS(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[a\S]", "a", true)]
    [InlineData(@"[a\S]", "z", true)]
    [InlineData(@"[a\S]", " ", false)]
    public void CharClassWithPositiveAndNegatedS(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 5. MIXED NEGATED SHORTHAND COMBINATIONS IN CLASSES
    // ================================================================

    [Theory]
    [InlineData(@"[\D\S]", "a", true)]   // \D ∪ \S = everything
    [InlineData(@"[\D\S]", "0", true)]
    [InlineData(@"[\D\S]", " ", true)]
    [InlineData(@"[\D\S]", "\t", true)]
    public void CharClassDSMatchesEverything(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[^\D\S]", "a", false)]   // NOT(\D ∪ \S) = nothing
    [InlineData(@"[^\D\S]", "0", false)]
    [InlineData(@"[^\D\S]", " ", false)]
    public void NegatedCharClassDSMatchesNothing(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[\W\S]", "a", true)]   // \W ∪ \S = everything
    [InlineData(@"[\W\S]", " ", true)]
    [InlineData(@"[\W\S]", "0", true)]
    public void CharClassWSMatchesEverything(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[^\W\S]", "a", false)]   // NOT(\W ∪ \S) = nothing
    [InlineData(@"[^\W\S]", " ", false)]
    [InlineData(@"[^\W\S]", "0", false)]
    public void NegatedCharClassWSMatchesNothing(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[\D\W]", "a", true)]     // letters are non-digits
    [InlineData(@"[\D\W]", " ", true)]      // space is non-digit and non-word
    [InlineData(@"[\D\W]", "_", true)]      // underscore is non-digit (in \D)
    [InlineData(@"[\D\W]", "0", false)]     // digit AND word char → not in \D, not in \W
    public void CharClassDWMatchesNonDigitsOrNonWord(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[^\D\W]", "0", true)]    // NOT(\D ∪ \W) = digits (because digits are in \w and not in \D)
    [InlineData(@"[^\D\W]", "5", true)]
    [InlineData(@"[^\D\W]", "a", false)]   // letter is in \D
    [InlineData(@"[^\D\W]", " ", false)]
    [InlineData(@"[^\D\W]", "_", false)]   // underscore is in \D
    public void NegatedCharClassDWMatchesOnlyDigits(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 6. [\d\D], [\s\S], [\w\W] — match-any patterns
    // ================================================================

    [Theory]
    [InlineData(@"[\d\D]", "5", true)]
    [InlineData(@"[\d\D]", "a", true)]
    [InlineData(@"[\d\D]", " ", true)]
    [InlineData(@"[\d\D]", "\n", true)]
    public void DigitOrNotDigitMatchesAnything(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[\s\S]", "a", true)]
    [InlineData(@"[\s\S]", " ", true)]
    [InlineData(@"[\s\S]", "\n", true)]
    public void WhitespaceOrNotWhitespaceMatchesAnything(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[\w\W]", "a", true)]
    [InlineData(@"[\w\W]", " ", true)]
    [InlineData(@"[\w\W]", "\n", true)]
    public void WordOrNotWordMatchesAnything(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 7. CHARACTER CLASS RANGES WITH ESCAPES
    // ================================================================

    [Theory]
    [InlineData(@"[\u0041-\u005A]", "A", true)]
    [InlineData(@"[\u0041-\u005A]", "Z", true)]
    [InlineData(@"[\u0041-\u005A]", "M", true)]
    [InlineData(@"[\u0041-\u005A]", "a", false)]
    public void CharClassRangeWithUnicodeEscapes(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[\u{41}-\u{5A}]", "A", true)]
    [InlineData(@"[\u{41}-\u{5A}]", "Z", true)]
    [InlineData(@"[\u{41}-\u{5A}]", "a", false)]
    public void CharClassRangeWithBraceUnicodeEscapes(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[\x41-\x5A]", "A", true)]
    [InlineData(@"[\x41-\x5A]", "Z", true)]
    [InlineData(@"[\x41-\x5A]", "a", false)]
    public void CharClassRangeWithHexEscapes(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 8. HYPHEN POSITIONING IN CHARACTER CLASSES
    // ================================================================

    [Theory]
    [InlineData("[-abc]", "-", true)]
    [InlineData("[-abc]", "a", true)]
    [InlineData("[-abc]", "d", false)]
    [InlineData("[abc-]", "-", true)]
    [InlineData("[abc-]", "a", true)]
    [InlineData("[abc-]", "d", false)]
    [InlineData("[-]", "-", true)]
    [InlineData("[-]", "a", false)]
    public void HyphenPositioning(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 9. ESCAPED CHARACTERS INSIDE CHARACTER CLASSES
    // ================================================================

    [Theory]
    [InlineData(@"[\]]", "]", true)]
    [InlineData(@"[\]]", "a", false)]
    [InlineData(@"[\[]", "[", true)]
    [InlineData(@"[\[]", "a", false)]
    [InlineData(@"[\\]", "\\", true)]
    [InlineData(@"[\\]", "a", false)]
    [InlineData(@"[\-]", "-", true)]
    [InlineData(@"[\-]", "a", false)]
    public void EscapedCharsInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 10. BACKSPACE IN CHARACTER CLASS ([\b])
    // ================================================================

    [Theory]
    [InlineData(@"[\b]", "\u0008", true)]  // backspace character
    [InlineData(@"[\b]", "a", false)]
    public void BackspaceInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 11. UNICODE PROPERTY ESCAPES IN CHARACTER CLASSES
    // ================================================================

    [Theory]
    [InlineData(@"[\P{L}]", "0", true)]
    [InlineData(@"[\P{L}]", " ", true)]
    [InlineData(@"[\P{L}]", "a", false)]
    public void NegatedPropertyInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[\p{L}\d]", "a", true)]
    [InlineData(@"[\p{L}\d]", "5", true)]
    [InlineData(@"[\p{L}\d]", " ", false)]
    public void PropertyAndShorthandInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[\p{Lu}a-z]", "A", true)]
    [InlineData(@"[\p{Lu}a-z]", "a", true)]
    [InlineData(@"[\p{Lu}a-z]", "5", false)]
    public void PropertyAndRangeInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"[^\p{L}]", "0", true)]
    [InlineData(@"[^\p{L}]", "a", false)]
    public void NegatedClassWithProperty(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 12. MULTIPLE RANGES IN CHARACTER CLASSES
    // ================================================================

    [Theory]
    [InlineData("[a-zA-Z0-9]", "a", true)]
    [InlineData("[a-zA-Z0-9]", "Z", true)]
    [InlineData("[a-zA-Z0-9]", "5", true)]
    [InlineData("[a-zA-Z0-9]", "_", false)]
    [InlineData("[a-zA-Z0-9]", " ", false)]
    public void MultipleRangesInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 13. NESTED GROUPS
    // ================================================================

    [Theory]
    [InlineData("((a)(b))", "ab", true)]
    [InlineData("((a)(b))", "ac", false)]
    [InlineData("((?:a)b)", "ab", true)]
    [InlineData("((?:a)b)", "cb", false)]
    public void NestedGroups(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 14. ALTERNATION INSIDE GROUPS
    // ================================================================

    [Theory]
    [InlineData("(a|b|c)", "a", true)]
    [InlineData("(a|b|c)", "b", true)]
    [InlineData("(a|b|c)", "c", true)]
    [InlineData("(a|b|c)", "d", false)]
    [InlineData("(?:a|b|c)", "a", true)]
    [InlineData("(a|bb|ccc)", "bb", true)]
    [InlineData("(a|bb|ccc)", "dd", false)]
    public void AlternationInsideGroups(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData("(a|(b|c))", "a", true)]
    [InlineData("(a|(b|c))", "b", true)]
    [InlineData("(a|(b|c))", "c", true)]
    [InlineData("(a|(b|c))", "d", false)]
    public void NestedAlternation(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 15. EMPTY ALTERNATION
    // ================================================================

    [Theory]
    [InlineData("a|", "a", true)]
    [InlineData("a|", "", true)]       // empty alternative matches empty string
    [InlineData("a|", "b", true)]      // empty alternative matches at start of 'b'
    [InlineData("|b", "b", true)]
    [InlineData("|b", "", true)]
    [InlineData("a||b", "", true)]
    [InlineData("a||b", "a", true)]
    [InlineData("a||b", "b", true)]
    public void EmptyAlternation(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 16. ALTERNATION SCOPE (precedence)
    // ================================================================

    [Theory]
    [InlineData("^a|b$", "a", true)]     // (^a) | (b$) — not ^(a|b)$
    [InlineData("^a|b$", "b", true)]
    [InlineData("^a|b$", "xb", true)]    // b$ matches
    [InlineData("^a|b$", "ax", true)]    // ^a matches
    public void AlternationScope(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 17. QUANTIFIER EDGE CASES
    // ================================================================

    [Theory]
    [InlineData("^a{0}$", "", true)]
    [InlineData("^a{0}$", "a", false)]
    [InlineData("^a{0,0}$", "", true)]
    [InlineData("^a{0,0}$", "a", false)]
    [InlineData("^a{0,1}$", "", true)]
    [InlineData("^a{0,1}$", "a", true)]
    [InlineData("^a{0,1}$", "aa", false)]
    [InlineData("a{0,}", "", true)]
    [InlineData("^a{3,5}$", "aaaaaa", false)]
    public void QuantifierEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData("(ab)+", "ababab", true)]
    [InlineData("(ab)+", "cd", false)]
    [InlineData("(?:ab){2}", "abab", true)]
    [InlineData("(?:ab){2}", "ab", false)]
    [InlineData("(a|b)+", "aabba", true)]
    [InlineData("(a|b)+", "cccc", false)]
    public void QuantifierOnGroups(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData("[a-z]+", "abc", true)]
    [InlineData("[a-z]+", "123", false)]
    [InlineData("[a-z]{2,4}", "ab", true)]
    [InlineData("^[a-z]{2,4}$", "abcde", false)]
    public void QuantifierOnCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // Lazy quantifiers
    [Theory]
    [InlineData("a*?", "", true)]
    [InlineData("a+?", "a", true)]
    [InlineData("a+?", "", false)]
    [InlineData("a{3,5}?", "aaa", true)]
    public void LazyQuantifiers(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 18. BACKREFERENCE EDGE CASES
    // ================================================================

    [Theory]
    [InlineData(@"(a)\1", "aa", true)]
    [InlineData(@"(a)\1", "ab", false)]
    [InlineData(@"(a)(b)\1\2", "abab", true)]
    [InlineData(@"(a)(b)\1\2", "abba", false)]
    public void MultipleBackreferences(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10", "abcdefghijj", true)]
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10", "abcdefghijk", false)]
    public void MultiDigitBackreferences(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 19. ASSERTION EDGE CASES
    // ================================================================

    [Theory]
    [InlineData("^$", "", true)]
    [InlineData("^$", "a", false)]
    public void EmptyStringAnchors(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData("q(?!u)", "qi", true)]
    [InlineData("q(?!u)", "qu", false)]
    public void NegativeLookahead(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData("(?<!a)b", "cb", true)]
    [InlineData("(?<!a)b", "ab", false)]
    public void NegativeLookbehind(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData("(?=a(?=b))", "ab", true)]
    [InlineData("(?=a(?=b))", "ac", false)]
    public void NestedLookaround(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData("a(?=)b", "ab", true)]     // empty positive lookahead always succeeds
    [InlineData("a(?!)b", "ab", false)]    // empty negative lookahead always fails
    public void EmptyLookaround(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"(?=.*\d)", "abc1", true)]
    [InlineData(@"(?=.*\d)", "abcd", false)]
    public void LookaheadWithShorthand(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 20. UNICODE ESCAPE EDGE CASES
    // ================================================================

    [Theory]
    [InlineData(@"\u{0}", "\0", true)]
    [InlineData(@"\u{0}", "A", false)]
    [InlineData(@"\u{FF}", "\u00FF", true)]
    [InlineData(@"\u{FFFF}", "\uFFFF", true)]
    [InlineData(@"\u{10000}", "\U00010000", true)]
    [InlineData(@"\u{10FFFF}", "\U0010FFFF", true)]
    public void UnicodeEscapeEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 21. CONTROL ESCAPE EDGE CASES
    // ================================================================

    [Theory]
    [InlineData(@"\cJ", "\n", true)]       // \cJ = LF
    [InlineData(@"\cI", "\t", true)]       // \cI = TAB
    [InlineData(@"\cA", "\u0001", true)]   // \cA = SOH
    [InlineData(@"\cZ", "\u001A", true)]   // \cZ = SUB
    public void ControlEscapeEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 22. HEX ESCAPE EDGE CASES
    // ================================================================

    [Theory]
    [InlineData(@"\x61", "a", true)]
    [InlineData(@"\x20", " ", true)]
    [InlineData(@"\x00", "\0", true)]
    public void HexEscapeEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 23. DOT WITH QUANTIFIERS
    // ================================================================

    [Theory]
    [InlineData(".+", "abc", true)]
    [InlineData(".+", "", false)]
    [InlineData(".*", "", true)]
    [InlineData(".{3}", "abc", true)]
    [InlineData("^.{3}$", "ab", false)]
    public void DotWithQuantifiers(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 24. ES2025 MODIFIER GROUPS (?i:...)
    // ================================================================

    [Theory]
    [InlineData("(?i:abc)", "ABC", true)]
    [InlineData("(?i:abc)", "abc", true)]
    [InlineData("(?i:abc)", "AbC", true)]
    [InlineData("(?i:abc)def", "ABCdef", true)]
    [InlineData("(?i:abc)def", "ABCDEF", false)]  // only group is case-insensitive
    [InlineData("a(?i:bc)d", "aBCd", true)]
    [InlineData("a(?i:bc)d", "ABCD", false)]      // 'a' and 'd' must match exactly
    public void ModifierGroups(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 25. CONSECUTIVE CHARACTER CLASSES
    // ================================================================

    [Theory]
    [InlineData("[a-z][0-9]", "a5", true)]
    [InlineData("[a-z][0-9]", "5a", false)]
    public void ConsecutiveCharClasses(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 26. MULTIPLE ESCAPES IN SEQUENCE
    // ================================================================

    [Theory]
    [InlineData(@"^\d\w\s$", "1a ", true)]
    [InlineData(@"^\d\w\s$", "aaa", false)]
    public void MultipleEscapesInSequence(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 27. COMBINED FEATURES — word boundary + shorthands
    // ================================================================

    [Theory]
    [InlineData(@"\b\w+\b\s+\w+", "hello world", true)]
    [InlineData(@"\b\w+\b\s+\w+", "hello", false)]
    public void CombinedBoundaryAndShorthands(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 28. UNICODE PROPERTY — Script= translation (pass-through validation)
    // Note: .NET uses Unicode block names (IsGreek, IsCyrillic) not script names.
    // The translator maps Script=X → \p{IsX}. This works for names that match
    // .NET block names (e.g., IsGreek, IsCyrillic) but not all (e.g., IsLatin).
    // ================================================================

    [Theory]
    [InlineData(@"\p{sc=Greek}", "\u03B1", true)]         // α (alpha) — IsGreek block
    [InlineData(@"\p{sc=Greek}", "a", false)]
    [InlineData(@"\p{Script_Extensions=Cyrillic}", "\u0410", true)]  // IsCyrillic block
    [InlineData(@"\p{Script_Extensions=Cyrillic}", "a", false)]
    public void PropertyScriptVariantsWithBlockNames(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 29. UNICODE PROPERTY — gc= variants
    // ================================================================

    [Theory]
    [InlineData(@"\p{gc=Nd}", "3", true)]
    [InlineData(@"\p{gc=Nd}", "a", false)]
    public void PropertyGcVariants(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 30. TRANSLATION TESTS — new patterns
    // ================================================================

    [Theory]
    [InlineData(@"[\S]", @"[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    [InlineData(@"[^\S]", @"[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    public void NegatedSShorthandInCharClassTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData(@"[a\S]", @"(?:[a]|[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000])")]
    public void MixedPositiveAndNegatedSTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData(@"[\D\S]", @"(?:[^0-9]|[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000])")]
    [InlineData(@"[\D\W]", "(?:[^0-9]|[^a-zA-Z0-9_])")]
    [InlineData(@"[\W\S]", @"(?:[^a-zA-Z0-9_]|[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000])")]
    public void MultipleNegatedShorthandsNonNegatedClassTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData(@"[^\D\S]", @"[^\s\S]")]       // empty: D+S in negated
    [InlineData(@"[^\W\S]", @"[^\s\S]")]       // empty: W+S in negated
    [InlineData(@"[^\D\W]", "[0-9]")]           // intersection of [0-9] and [a-zA-Z0-9_]
    public void MultipleNegatedShorthandsNegatedClassTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData(@"[\d\D]", "(?:[0-9]|[^0-9])")]
    [InlineData(@"[\s\S]", @"(?:[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]|[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000])")]
    [InlineData(@"[\w\W]", "(?:[a-zA-Z0-9_]|[^a-zA-Z0-9_])")]
    public void ComplementPairsInClassTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData(@"[\P{L}]", @"[\P{L}]")]
    [InlineData(@"[\p{L}\d]", @"[\p{L}0-9]")]
    [InlineData(@"[\p{Lu}a-z]", @"[\p{Lu}a-z]")]
    [InlineData(@"[^\p{L}]", @"[^\p{L}]")]
    public void PropertyEscapesInCharClassTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData(@"\u{0}", @"\u0000")]
    [InlineData(@"\u{A}", @"\u000A")]
    [InlineData(@"\u{FF}", @"\u00FF")]
    [InlineData(@"\u{100}", @"\u0100")]
    [InlineData(@"\u{10000}", @"(?:\uD800\uDC00)")]
    public void UnicodeEscapeEdgeCasesTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData("(?i:abc)", "(?i:abc)")]
    [InlineData("a(?i:bc)d", "a(?i:bc)d")]
    [InlineData("(?i:abc)def", "(?i:abc)def")]
    public void ModifierGroupsPassThroughTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData("a|", "a|")]
    [InlineData("|b", "|b")]
    [InlineData("a||b", "a||b")]
    public void EmptyAlternationPassThroughTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData(@"(a)(b)\1\2", @"(a)(b)(?(1)\1)(?(2)\2)")]
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10", @"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(?(10)\10)")]
    public void MultiDigitBackrefTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData("((a)(b))", "((a)(b))")]
    [InlineData("((?:a)b)", "((?:a)b)")]
    [InlineData("(a|(b|c))", "(a|(b|c))")]
    public void NestedGroupsPassThroughTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData("a{0}", "a{0}")]
    [InlineData("a{0,0}", "a{0,0}")]
    [InlineData("a{0,1}", "a{0,1}")]
    [InlineData("a{0,}", "a{0,}")]
    public void QuantifierEdgeCasesPassThroughTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData("[-abc]", "[-abc]")]
    [InlineData("[abc-]", "[abc-]")]
    [InlineData("[-]", "[-]")]
    public void HyphenPositionPassThroughTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData(@"[\]]", @"[\]]")]
    [InlineData(@"[\[]", @"[\[]")]
    [InlineData(@"[\\]", @"[\\]")]
    [InlineData(@"[\-]", @"[\-]")]
    public void EscapedCharsInClassPassThroughTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData("(?=a(?=b))", "(?=a(?=b))")]
    [InlineData("(?=a(?!b))", "(?=a(?!b))")]
    [InlineData("a(?=)b", "a(?=)b")]
    [InlineData("a(?!)b", "a(?!)b")]
    public void NestedLookaroundPassThroughTranslation(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ================================================================
    // 31. SPAN API — TryTranslate with comprehensive patterns
    // ================================================================

    [Fact]
    public void TryTranslateCharClassWithNegatedShorthand()
    {
        ReadOnlySpan<char> ecma = @"[\S]";
        Span<char> buffer = stackalloc char[256];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out int written);

        Assert.Equal(OperationStatus.Done, status);
        string result = new(buffer[..written]);
        Assert.Contains(@"\t\n\v\f\r", result);
    }

    [Fact]
    public void TryTranslateModifierGroup()
    {
        ReadOnlySpan<char> ecma = "(?i:abc)";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out int written);

        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal("(?i:abc)", new string(buffer[..written]));
    }

    [Fact]
    public void TryTranslateMultiDigitBackref()
    {
        ReadOnlySpan<char> ecma = @"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10";
        Span<char> buffer = stackalloc char[256];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out int written);

        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(?(10)\10)", new string(buffer[..written]));
    }

    [Fact]
    public void GetMaxTranslatedLengthHandlesComplexPattern()
    {
        string ecma = @"[\D\S\w]+\b\p{Letter}\u{1F600}";
        int maxLen = EcmaRegexTranslator.GetMaxTranslatedLength(ecma);
        string result = EcmaRegexTranslator.Translate(ecma);

        Assert.True(maxLen >= result.Length, $"MaxLen {maxLen} < actual {result.Length}");
    }

    // ================================================================
    // 32. ERROR CASES — invalid input
    // ================================================================

    [Fact]
    public void InvalidUnicodeEscapeReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\u{GGGG}";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.Equal(OperationStatus.InvalidData, status);
    }

    [Fact]
    public void UnclosedUnicodeBraceReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\u{41";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.Equal(OperationStatus.InvalidData, status);
    }

    [Fact]
    public void TruncatedHexEscapeReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\x4";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.Equal(OperationStatus.InvalidData, status);
    }

    [Fact]
    public void TruncatedControlEscapeReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\c";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.Equal(OperationStatus.InvalidData, status);
    }

    [Fact]
    public void UnclosedPropertyEscapeReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\p{L";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.Equal(OperationStatus.InvalidData, status);
    }

    [Fact]
    public void UnclosedCharacterClassReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = "[abc";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.Equal(OperationStatus.InvalidData, status);
    }

    [Fact]
    public void TruncatedUnicodeEscapeReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\u00";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.Equal(OperationStatus.InvalidData, status);
    }

    [Fact]
    public void CodePointAboveMaxReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\u{110000}";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.Equal(OperationStatus.InvalidData, status);
    }

    // ================================================================
    // 33. BACKREFERENCE VS OCTAL AMBIGUITY
    // ECMAScript /u mode completely forbids octal escapes:
    //   - \0 is valid only when NOT followed by a digit
    //   - \1-\9 are always backreferences (error if group doesn't exist)
    //   - \01, \07, \08 etc. are all SyntaxErrors
    // The translator must reject \0 followed by a digit to prevent .NET
    // from silently interpreting it as an octal escape.
    // ================================================================

    // --- \0 valid cases (not followed by digit) ---

    [Theory]
    [InlineData(@"\0", @"\0")]              // NUL at end of pattern
    [InlineData(@"\0a", @"\0a")]            // NUL followed by letter
    [InlineData(@"\0\n", @"\0\n")]          // NUL followed by escape
    [InlineData(@"x\0y", @"x\0y")]         // NUL in middle of pattern
    public void NullEscapeNotFollowedByDigitTranslatesCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData(@"\0", "\0", true)]         // NUL matches NUL
    [InlineData(@"\0", "0", false)]         // NUL does not match '0'
    [InlineData(@"\0", "a", false)]         // NUL does not match 'a'
    [InlineData(@"\0a", "\0a", true)]       // NUL then literal 'a'
    public void NullEscapeMatchesNullCharacter(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // --- \0 followed by digit = INVALID in /u mode (octal forbidden) ---

    [Theory]
    [InlineData(@"\00")]     // \0 + '0'
    [InlineData(@"\01")]     // \0 + '1' — would be octal 1 in .NET
    [InlineData(@"\02")]     // \0 + '2'
    [InlineData(@"\03")]     // \0 + '3'
    [InlineData(@"\07")]     // \0 + '7' — highest single-digit octal
    [InlineData(@"\08")]     // \0 + '8' — not even valid octal, still forbidden
    [InlineData(@"\09")]     // \0 + '9'
    public void NullEscapeFollowedByDigitIsInvalid(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);

        Assert.Equal(OperationStatus.InvalidData, status);
    }

    // --- \0 followed by digit inside character class = INVALID ---

    [Theory]
    [InlineData(@"[\01]")]     // \0 + '1' inside class
    [InlineData(@"[\07]")]     // \0 + '7' inside class
    [InlineData(@"[\09]")]     // \0 + '9' inside class
    public void NullEscapeFollowedByDigitInCharClassIsInvalid(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);

        Assert.Equal(OperationStatus.InvalidData, status);
    }

    // --- \0 in character class without trailing digit = valid ---

    [Theory]
    [InlineData(@"[\0]", "\0", true)]       // NUL in char class
    [InlineData(@"[\0]", "a", false)]
    [InlineData(@"[\0a]", "\0", true)]      // NUL or 'a' class
    [InlineData(@"[\0a]", "a", true)]
    [InlineData(@"[\0a]", "b", false)]
    public void NullEscapeInCharClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // --- Backreferences (NOT octal) in /u mode ---

    [Theory]
    [InlineData(@"(a)\1", @"(a)(?(1)\1)")]                  // single digit backref
    [InlineData(@"(a)(b)\2", @"(a)(b)(?(2)\2)")]             // second group
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)\8", @"(a)(b)(c)(d)(e)(f)(g)(h)(?(8)\8)")]   // \8 = backref
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)\9", @"(a)(b)(c)(d)(e)(f)(g)(h)(i)(?(9)\9)")]  // \9 = backref
    public void SingleDigitBackrefsAreNeverOctal(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData(@"(a)\1", "aa", true)]
    [InlineData(@"(a)\1", "ab", false)]
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)\8", "abcdefghh", true)]
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)\8", "abcdefghx", false)]
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)\9", "abcdefghii", true)]
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)\9", "abcdefghix", false)]
    public void SingleDigitBackrefsMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // --- Multi-digit backreferences ---

    [Theory]
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10", "abcdefghijj", true)]
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10", "abcdefghijk", false)]
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)\11", "abcdefghijkk", true)]
    [InlineData(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)\11", "abcdefghijkx", false)]
    public void MultiDigitBackrefsMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 34. STRICT IDENTITY ESCAPE VALIDATION — /u mode
    // In ECMAScript /u mode, only SyntaxCharacter and / can be
    // identity-escaped outside character classes. Inside classes,
    // - is also valid. All other identity escapes are SyntaxErrors.
    // ================================================================

    // --- Invalid identity escapes outside character class ---
    // These are CRITICAL: e.g. \a in .NET means BEL (U+0007),
    // but in ES /u mode \a is a SyntaxError. Passing through
    // would silently match the wrong character.

    [Theory]
    [InlineData(@"\a")]      // would be BEL in .NET!
    [InlineData(@"\e")]      // would be ESC in .NET!
    [InlineData(@"\q")]
    [InlineData(@"\i")]
    [InlineData(@"\m")]
    [InlineData(@"\o")]
    [InlineData(@"\y")]
    [InlineData(@"\z")]
    [InlineData(@"\g")]
    [InlineData(@"\h")]
    [InlineData(@"\j")]
    [InlineData(@"\l")]
    [InlineData(@"\A")]
    [InlineData(@"\C")]
    [InlineData(@"\E")]
    [InlineData(@"\F")]
    [InlineData(@"\G")]
    [InlineData(@"\H")]
    [InlineData(@"\I")]
    [InlineData(@"\J")]
    [InlineData(@"\K")]
    [InlineData(@"\L")]
    [InlineData(@"\M")]
    [InlineData(@"\N")]
    [InlineData(@"\O")]
    [InlineData(@"\Q")]
    [InlineData(@"\R")]
    [InlineData(@"\T")]
    [InlineData(@"\U")]
    [InlineData(@"\V")]
    [InlineData(@"\X")]
    [InlineData(@"\Y")]
    [InlineData(@"\Z")]
    public void InvalidIdentityEscapeOutsideClassReturnsInvalidData(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);
        Assert.Equal(OperationStatus.InvalidData, status);
    }

    [Fact]
    public void EscapedHyphenOutsideClassReturnsInvalidData()
    {
        // \- is only valid inside character classes in /u mode
        ReadOnlySpan<char> pattern = @"\-";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);
        Assert.Equal(OperationStatus.InvalidData, status);
    }

    // --- Valid identity escapes (SyntaxCharacter + /) ---
    [Theory]
    [InlineData(@"\^", @"\^")]
    [InlineData(@"\$", @"\$")]
    [InlineData(@"\.", @"\.")]
    [InlineData(@"\*", @"\*")]
    [InlineData(@"\+", @"\+")]
    [InlineData(@"\?", @"\?")]
    [InlineData(@"\(", @"\(")]
    [InlineData(@"\)", @"\)")]
    [InlineData(@"\[", @"\[")]
    [InlineData(@"\]", @"\]")]
    [InlineData(@"\{", @"\{")]
    [InlineData(@"\}", @"\}")]
    [InlineData(@"\|", @"\|")]
    [InlineData(@"\\", @"\\")]
    [InlineData(@"\/", @"\/")]
    public void ValidIdentityEscapeTranslatesCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // --- Valid identity escapes match literally ---
    [Theory]
    [InlineData(@"\^", "^", true)]
    [InlineData(@"\^", "a", false)]
    [InlineData(@"\$", "$", true)]
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
    public void ValidIdentityEscapeMatchesLiteral(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 35. INVALID ESCAPES INSIDE CHARACTER CLASS — /u mode
    // ================================================================

    [Theory]
    [InlineData(@"[\a]")]      // \a invalid in class
    [InlineData(@"[\e]")]      // \e invalid in class
    [InlineData(@"[\q]")]      // \q invalid in class
    [InlineData(@"[\B]")]      // \B invalid in class (only \b = backspace is valid)
    [InlineData(@"[\A]")]
    [InlineData(@"[\E]")]
    [InlineData(@"[\G]")]
    [InlineData(@"[\i]")]
    [InlineData(@"[\j]")]
    [InlineData(@"[\l]")]
    [InlineData(@"[\m]")]
    [InlineData(@"[\o]")]
    [InlineData(@"[\y]")]
    [InlineData(@"[\z]")]
    public void InvalidIdentityEscapeInCharClassReturnsInvalidData(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);
        Assert.Equal(OperationStatus.InvalidData, status);
    }

    [Theory]
    [InlineData(@"(a)[\1]")]     // backreference in class (digit escape in class)
    [InlineData(@"[\9]")]        // digit escape in class (no group exists, but invalid regardless)
    public void BackreferenceInCharClassReturnsInvalidData(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);
        Assert.Equal(OperationStatus.InvalidData, status);
    }

    // --- Valid escapes in character class ---
    [Theory]
    [InlineData(@"[\-]", @"[\-]")]         // \- valid inside class
    [InlineData(@"[\^]", @"[\^]")]         // \^ valid inside class
    [InlineData(@"[\]]", @"[\]]")]         // \] valid inside class
    [InlineData(@"[\\]", @"[\\]")]         // \\ valid inside class
    [InlineData(@"[\/]", @"[\/]")]         // \/ valid inside class
    [InlineData(@"[\.]", @"[\.]")]         // \. valid inside class
    [InlineData(@"[\t]", @"[\t]")]         // \t control escape in class
    [InlineData(@"[\n]", @"[\n]")]         // \n control escape in class
    [InlineData(@"[\r]", @"[\r]")]         // \r control escape in class
    [InlineData(@"[\f]", @"[\f]")]         // \f control escape in class
    [InlineData(@"[\v]", @"[\v]")]         // \v control escape in class
    public void ValidEscapeInCharClassTranslatesCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ================================================================
    // 36. CONTROL ESCAPE VALIDATION — /u mode
    // ================================================================

    [Theory]
    [InlineData(@"\c0")]       // digit not valid
    [InlineData(@"\c!")]       // punctuation not valid
    [InlineData(@"\c ")]       // space not valid
    [InlineData(@"\c1")]
    [InlineData(@"\c9")]
    public void InvalidControlLetterReturnsInvalidData(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);
        Assert.Equal(OperationStatus.InvalidData, status);
    }

    [Theory]
    [InlineData(@"\cA")]      // valid: uppercase
    [InlineData(@"\cZ")]      // valid: uppercase
    [InlineData(@"\ca")]      // valid: lowercase
    [InlineData(@"\cz")]      // valid: lowercase
    [InlineData(@"\cJ")]      // Control-J = \n
    [InlineData(@"\cM")]      // Control-M = \r
    [InlineData(@"\cI")]      // Control-I = \t
    public void ValidControlLetterTranslatesCorrectly(string ecma)
    {
        Assert.Equal(ecma, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData(@"\cA", "\x01", true)]    // Control-A
    [InlineData(@"\cZ", "\x1A", true)]    // Control-Z
    [InlineData(@"\ca", "\x01", true)]    // lowercase = same
    [InlineData(@"\cJ", "\n", true)]      // Control-J = LF
    [InlineData(@"\cM", "\r", true)]      // Control-M = CR
    [InlineData(@"\cI", "\t", true)]      // Control-I = TAB
    [InlineData(@"\cA", "A", false)]
    public void ControlEscapeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // Invalid control letter inside character class
    [Theory]
    [InlineData(@"[\c0]")]
    [InlineData(@"[\c!]")]
    public void InvalidControlLetterInCharClassReturnsInvalidData(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);
        Assert.Equal(OperationStatus.InvalidData, status);
    }

    // ================================================================
    // 37. DOT VS LINE TERMINATORS — ES excludes all four
    // ================================================================

    [Theory]
    [InlineData(".", "\n", false)]         // LF
    [InlineData(".", "\r", false)]         // CR
    [InlineData(".", "\u2028", false)]     // Line Separator
    [InlineData(".", "\u2029", false)]     // Paragraph Separator
    [InlineData(".", "\t", true)]          // TAB is fine
    [InlineData(".", "\u00A0", true)]      // NBSP is fine
    [InlineData(".", "\uFEFF", true)]      // BOM is fine
    [InlineData(".", "a", true)]
    public void DotExcludesAllFourLineTerminators(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 38. SUPPLEMENTARY CHARACTER MATCHING
    // ================================================================

    [Theory]
    [InlineData(@"\u{1F600}", "\U0001F600", true)]     // 😀
    [InlineData(@"\u{1F600}", "a", false)]
    [InlineData(@"\u{10000}", "\U00010000", true)]      // first supplementary
    [InlineData(@"\u{10FFFF}", "\U0010FFFF", true)]     // max codepoint
    [InlineData(@"\u{FFFF}", "\uFFFF", true)]           // BMP boundary
    [InlineData(@"\u{0}", "\0", true)]                   // NUL via braced syntax
    [InlineData(@"\u{41}", "A", true)]                   // simple BMP
    public void SupplementaryCharacterMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // Dot matches supplementary character as single grapheme in .NET
    [Fact]
    public void DotMatchesSingleSupplementaryCharacter()
    {
        AssertMatch(".", "\U0001F600", true);
    }

    // Supplementary character range — known limitation:
    // .NET cannot represent surrogate pair ranges in character classes.
    // [\u{1F600}-\u{1F64F}] translates to surrogate-pair-aware alternation
    // e.g. \uD83D[\uDE00-\uDE4F] for same-high-surrogate ranges.

    [Theory]
    [InlineData(@"[\u{1F600}-\u{1F64F}]", "\U0001F600", true)]     // 😀 start
    [InlineData(@"[\u{1F600}-\u{1F64F}]", "\U0001F64F", true)]     // 🙏 end
    [InlineData(@"[\u{1F600}-\u{1F64F}]", "\U0001F300", false)]    // 🌀 outside
    [InlineData(@"[\u{1F600}-\u{1F64F}]", "A", false)]
    public void SupplementaryCharacterRangeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 40. CHARACTER CLASS WITH \u{} RANGES (BMP only)
    // ================================================================

    [Theory]
    [InlineData(@"\u{41}", @"\u0041")]                 // simple BMP
    [InlineData(@"\u{0}", @"\u0000")]                  // zero
    [InlineData(@"\u{FF}", @"\u00FF")]                 // two hex digits
    [InlineData(@"\u{FFFF}", @"\uFFFF")]               // BMP max
    [InlineData(@"\u{10000}", @"(?:\uD800\uDC00)")]        // first supplementary → surrogate pair in group
    [InlineData(@"\u{10FFFF}", @"(?:\uDBFF\uDFFF)")]       // max → surrogate pair in group
    [InlineData(@"\u{1F600}", @"(?:\uD83D\uDE00)")]        // emoji → surrogate pair in group
    public void UnicodeEscapeBoundaryValuesTranslateCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ================================================================
    // 40. CHARACTER CLASS WITH \u{} RANGES
    // ================================================================

    [Theory]
    [InlineData(@"[\u{41}-\u{5A}]", "A", true)]     // A-Z
    [InlineData(@"[\u{41}-\u{5A}]", "Z", true)]
    [InlineData(@"[\u{41}-\u{5A}]", "a", false)]
    [InlineData(@"[\u{41}-\u{5A}]", "@", false)]
    public void UnicodeEscapeInCharClassRangeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // Mixed \u{} and literal in class
    [Theory]
    [InlineData(@"[\u{41}bc]", "A", true)]
    [InlineData(@"[\u{41}bc]", "b", true)]
    [InlineData(@"[\u{41}bc]", "c", true)]
    [InlineData(@"[\u{41}bc]", "d", false)]
    public void MixedUnicodeEscapeAndLiteralInClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 41. FORWARD REFERENCE AND NON-PARTICIPATING GROUP
    // Known limitation: In ECMAScript, forward references and
    // non-participating group backreferences match the empty string.
    // In .NET, unset group backreferences fail to match.
    // This is a fundamental semantic difference between the engines.
    // ================================================================

    [Theory]
    [InlineData(@"\1(a)", "a", true)]         // forward ref = empty match, then captures
    public void ForwardReferenceMatchesEmptyString(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"(?:a|(b))\1", "a", true)]   // group 1 didn't participate → \1 = empty
    [InlineData(@"(?:a|(b))\1", "bb", true)]  // group 1 = 'b', \1 = 'b'
    public void NonParticipatingGroupBackrefMatchesEmpty(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 42. HEX ESCAPE VALUES
    // ================================================================

    [Theory]
    [InlineData(@"\x41", "A", true)]
    [InlineData(@"\x61", "a", true)]
    [InlineData(@"\x00", "\0", true)]
    [InlineData(@"\xFF", "\xFF", true)]
    [InlineData(@"\x41", "B", false)]
    public void HexEscapeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 43. BACKSPACE IN CHARACTER CLASS
    // ================================================================

    [Theory]
    [InlineData(@"[\b]", "\x08", true)]     // U+0008 backspace
    [InlineData(@"[\b]", "b", false)]
    [InlineData(@"[\b]", "\n", false)]
    public void BackspaceInCharClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 44. COMPLEX NESTED GROUPS WITH BACKREFERENCES
    // ================================================================

    [Theory]
    [InlineData(@"((a)(b))\1\2\3", "ababab", true)]
    [InlineData(@"((a)(b))\1\2\3", "ababba", false)]
    public void NestedGroupBackrefsMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // Backreference with quantifier
    [Theory]
    [InlineData(@"(a)\1+", "aa", true)]
    [InlineData(@"(a)\1+", "aaa", true)]
    [InlineData(@"(a)\1+", "a", false)]       // \1 must match once
    [InlineData(@"(a)\1{2}", "aaa", true)]    // \1 must match twice
    [InlineData(@"(a)\1{2}", "aa", false)]
    public void BackreferenceWithQuantifierMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 45. LOOKAHEAD/LOOKBEHIND WITH SHORTHANDS
    // ================================================================

    [Theory]
    [InlineData(@"(?=\d)5", "5", true)]
    [InlineData(@"(?=\d)a", "a", false)]
    [InlineData(@"(?<=\d)a", "5a", true)]
    [InlineData(@"(?<=\d)a", "ba", false)]
    [InlineData(@"(?!\w)!", "!", true)]
    public void LookAroundWithShorthandsMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 46. PROPERTY ESCAPE INSIDE CHARACTER CLASS
    // ================================================================

    [Theory]
    [InlineData(@"[\p{L}\d]", "a", true)]     // \p{L} matches letter
    [InlineData(@"[\p{L}\d]", "5", true)]     // \d matches digit
    [InlineData(@"[\p{L}\d]", "!", false)]
    [InlineData(@"[\p{Lu}0-9]", "A", true)]   // uppercase letter
    [InlineData(@"[\p{Lu}0-9]", "5", true)]   // digit range
    [InlineData(@"[\p{Lu}0-9]", "a", false)]  // lowercase → no match
    public void PropertyEscapeInCharClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 47. EMPTY PATTERN AND QUANTIFIER EDGE CASES
    // ================================================================

    [Theory]
    [InlineData("", "", true)]           // empty pattern matches empty
    [InlineData("", "a", true)]          // empty pattern matches anywhere
    [InlineData("a{0}", "", true)]       // zero-match quantifier
    [InlineData("a{0,0}", "", true)]     // explicit zero-zero
    [InlineData("a{0,0}b", "b", true)]  // zero 'a's then 'b'
    public void EmptyAndZeroQuantifierEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 48. ESCAPED SPECIAL CHARS MATCH LITERALLY
    // ================================================================

    [Theory]
    [InlineData(@"\t", "\t", true)]
    [InlineData(@"\t", "t", false)]
    [InlineData(@"\n", "\n", true)]
    [InlineData(@"\r", "\r", true)]
    [InlineData(@"\f", "\f", true)]
    [InlineData(@"\v", "\v", true)]
    public void ControlEscapeCharsMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 49. \p{Nd} vs \d — DIFFERENT CHARACTER SETS
    // ================================================================

    [Theory]
    [InlineData(@"\p{Nd}", "\u0660", true)]    // Arabic-Indic digit ٠
    [InlineData(@"\d", "\u0660", false)]        // ES \d = [0-9] only
    [InlineData(@"\p{Nd}", "5", true)]
    [InlineData(@"\d", "5", true)]
    [InlineData(@"\w", "\u00E9", false)]        // é not in ASCII \w
    [InlineData(@"\w", "\u00F1", false)]        // ñ not in ASCII \w
    [InlineData(@"\d", "\uFF10", false)]        // fullwidth zero not in ES \d
    public void UnicodePropertyVsShorthandDifference(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 50. ORPHAN BACKREFERENCE — no groups defined
    // ================================================================

    [Theory]
    [InlineData(@"\1")]       // no group 1
    [InlineData(@"\2")]       // no group 2
    [InlineData(@"\9")]       // no group 9
    public void OrphanBackreferenceIsInvalid(string ecma)
    {
        // In ES /u mode, orphan backreferences (\N with no group N)
        // are SyntaxErrors. We can't fully validate this without
        // counting groups, but .NET will reject these at compile time.
        // This test documents the expected behavior.
        string dotNetPattern = EcmaRegexTranslator.Translate(ecma);
        Assert.Throws<System.Text.RegularExpressions.RegexParseException>(
            () => new Regex(dotNetPattern));
    }

    // ================================================================
    // 51. BACKREFERENCES WITH QUANTIFIERS (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"(a)\1+", "aa", true)]       // backref repeated 1+
    [InlineData(@"(a)\1+", "aaa", true)]      // backref repeated 2+
    [InlineData(@"(a)\1+", "a", false)]       // needs at least one repetition
    [InlineData(@"(a)\1?", "a", true)]        // optional backref = empty
    [InlineData(@"(a)\1?", "aa", true)]       // optional backref = matched
    [InlineData(@"(a)\1{2}", "aaa", true)]    // exact repeat
    [InlineData(@"(a)\1{2}", "aa", false)]    // too few
    [InlineData(@"(a)\1*", "a", true)]        // zero repeats
    [InlineData(@"(a)\1*", "aaaa", true)]     // many repeats
    [InlineData(@"(a)\1{1,3}", "aa", true)]   // range min
    [InlineData(@"(a)\1{1,3}", "aaaa", true)] // range max
    public void BackrefWithQuantifierMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 52. MULTIPLE BACKREFS TO SAME GROUP (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"(a)\1\1", "aaa", true)]
    [InlineData(@"(a)\1\1", "aa", false)]
    [InlineData(@"(ab)\1\1", "ababab", true)]
    public void MultipleBackrefsToSameGroupMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 53. BACKREFS INSIDE ALTERNATION (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"(a)(\1|b)", "aa", true)]
    [InlineData(@"(a)(\1|b)", "ab", true)]
    [InlineData(@"(a)(b|\1)", "aa", true)]
    [InlineData(@"(a)(b|\1)", "ab", true)]
    public void BackrefInsideAlternationMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 54. BACKREFS INSIDE LOOKAHEAD/LOOKBEHIND (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"(a)(?=\1)a", "aa", true)]
    [InlineData(@"(a)(?=\1)b", "ab", false)]
    [InlineData(@"(a)(?!\1)b", "ab", true)]
    [InlineData(@"(a)(?!\1)a", "aa", false)]
    public void BackrefInLookaroundMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 55. NESTED GROUPS WITH MULTIPLE BACKREFS (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"((a))\1\2", "aaa", true)]
    [InlineData(@"((a))\1\2", "aa", false)]
    [InlineData(@"((a)(b))\1\2\3", "ababab", true)]
    public void NestedGroupMultiBackrefsMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 56. QUANTIFIED GROUP WITH BACKREF (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"(a+)\1", "aa", true)]
    [InlineData(@"(a+)\1", "aaaa", true)]
    [InlineData(@"(a+)\1", "aaa", true)]
    [InlineData(@"(a{2})\1", "aaaa", true)]
    [InlineData(@"(a{2})\1", "aaa", false)]
    public void QuantifiedGroupBackrefMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 57. NAMED BACKREFS WITH VARIOUS NAMES (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"(?<name123>a)\k<name123>", "aa", true)]
    [InlineData(@"(?<n>a)\k<n>", "aa", true)]
    [InlineData(@"(?<longGroupName>a)\k<longGroupName>", "aa", true)]
    public void NamedBackrefVariationsMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 58. NON-PARTICIPATING / FORWARD BACKREF EDGE CASES (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"(a)?\1", "", true)]         // optional group didn't capture → empty
    [InlineData(@"(a)?\1", "aa", true)]       // optional group captured
    [InlineData(@"\1+(a)", "a", true)]        // forward ref with quantifier
    [InlineData(@"(?:a|(b))\1c", "ac", true)] // non-participating then literal
    [InlineData(@"(?:a|(b))\1c", "bbc", true)]// participating then literal
    public void NonParticipatingAndForwardBackrefEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 59. REAL-WORLD NAMED BACKREF PATTERNS (JS-validated)
    // ================================================================

    [Theory]
    [InlineData("(?<quote>[\"']).*?\\k<quote>", "\"hello\"", true)]
    [InlineData("(?<quote>[\"']).*?\\k<quote>", "'world'", true)]
    [InlineData("(?<quote>[\"']).*?\\k<quote>", "\"hello'", false)]
    public void QuoteMatchingWithNamedBackrefWorksCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 60. BACKREF TRANSLATION FORMAT (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"(a)\1+", @"(a)(?(1)\1)+")]
    [InlineData(@"(a)\1{2}", @"(a)(?(1)\1){2}")]
    [InlineData(@"(a)\1\1", @"(a)(?(1)\1)(?(1)\1)")]
    [InlineData(@"((a))\1\2", @"((a))(?(1)\1)(?(2)\2)")]
    [InlineData(@"(?<name123>a)\k<name123>", @"(?<name123>a)(?(name123)\k<name123>)")]
    public void BackrefTranslationFormatIsCorrect(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ================================================================
    // 61. MULTIPLE SUPPLEMENTARY RANGES IN ONE CLASS (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[\u{1F600}-\u{1F64F}\u{1F900}-\u{1F9FF}]", "\U0001F600", true)]   // 😀 first range
    [InlineData(@"[\u{1F600}-\u{1F64F}\u{1F900}-\u{1F9FF}]", "\U0001F923", true)]   // 🤣 second range
    [InlineData(@"[\u{1F600}-\u{1F64F}\u{1F900}-\u{1F9FF}]", "\U0001F300", false)]  // 🌀 outside both
    public void MultipleSupplementaryRangesInClassMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 62. MIXED BMP AND SUPPLEMENTARY IN CLASS (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[a-z\u{1F600}-\u{1F64F}]", "a", true)]
    [InlineData(@"[a-z\u{1F600}-\u{1F64F}]", "\U0001F600", true)]
    [InlineData(@"[a-z\u{1F600}-\u{1F64F}]", "0", false)]
    public void MixedBmpAndSupplementaryInClassMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 63. SINGLE SUPPLEMENTARY IN CLASS (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[\u{1F600}]", "\U0001F600", true)]
    [InlineData(@"[\u{1F600}]", "\U0001F601", false)]
    [InlineData(@"[\u{1F600}]", "a", false)]
    public void SingleSupplementaryInClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 64. TWO INDIVIDUAL SUPPLEMENTARY CHARS IN CLASS (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[\u{1F600}\u{1F601}]", "\U0001F600", true)]
    [InlineData(@"[\u{1F600}\u{1F601}]", "\U0001F601", true)]
    [InlineData(@"[\u{1F600}\u{1F601}]", "\U0001F602", false)]
    public void TwoSupplementaryCharsInClassMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 65. BMP CONTENT SURROUNDING SUPPLEMENTARY RANGE (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[A\u{1F600}-\u{1F64F}Z]", "A", true)]
    [InlineData(@"[A\u{1F600}-\u{1F64F}Z]", "\U0001F600", true)]
    [InlineData(@"[A\u{1F600}-\u{1F64F}Z]", "Z", true)]
    [InlineData(@"[A\u{1F600}-\u{1F64F}Z]", "B", false)]
    public void BmpAroundSupplementaryRangeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 66. ESCAPED CHARS WITH SUPPLEMENTARY (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[\-\u{1F600}-\u{1F64F}]", "-", true)]
    [InlineData(@"[\-\u{1F600}-\u{1F64F}]", "\U0001F600", true)]
    public void EscapedCharsWithSupplementaryMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 67. WIDE SUPPLEMENTARY RANGE (DIFFERENT HIGH SURROGATES) (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[\u{1F600}-\u{1F9FF}]", "\U0001F600", true)]   // start
    [InlineData(@"[\u{1F600}-\u{1F9FF}]", "\U0001F9FF", true)]   // end
    [InlineData(@"[\u{1F600}-\u{1F9FF}]", "\U0001F700", true)]   // middle
    [InlineData(@"[\u{1F600}-\u{1F9FF}]", "\U0001F300", false)]  // before
    [InlineData(@"[\u{1F600}-\u{1F9FF}]", "\U0001FA00", false)]  // after
    public void WideSupplementaryRangeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 68. FULL SUPPLEMENTARY PLANE RANGE (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[\u{10000}-\u{10FFFF}]", "\U00010000", true)]   // first supplementary
    [InlineData(@"[\u{10000}-\u{10FFFF}]", "\U0010FFFF", true)]   // last valid code point
    [InlineData(@"[\u{10000}-\u{10FFFF}]", "\U0001F600", true)]   // emoji in range
    [InlineData(@"[\u{10000}-\u{10FFFF}]", "A", false)]           // BMP
    public void FullSupplementaryRangeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 69. NEGATED SUPPLEMENTARY CLASS (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[^\u{1F600}]", "a", true)]
    [InlineData(@"[^\u{1F600}]", "\U0001F600", false)]
    public void NegatedSingleSupplementaryMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // Negated supplementary ranges use negative lookahead:
    // [^\u{1F600}-\u{1F64F}] → (?!\uD83D[\uDE00-\uDE4F])(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^\uD800-\uDFFF])
    [Theory]
    [InlineData(@"[^\u{1F600}-\u{1F64F}]", "a", true)]
    [InlineData(@"[^\u{1F600}-\u{1F64F}]", "\U0001F600", false)]
    [InlineData(@"[^\u{1F600}-\u{1F64F}]", "\U0001F300", true)]
    public void NegatedSupplementaryRangeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 70. \p{...} WITH SUPPLEMENTARY IN CLASS (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[\p{L}\u{1F600}]", "a", true)]
    [InlineData(@"[\p{L}\u{1F600}]", "\U0001F600", true)]
    [InlineData(@"[\p{L}\u{1F600}]", "5", false)]
    public void PropertyEscapeWithSupplementaryInClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 71. SHORTHAND WITH SUPPLEMENTARY IN CLASS (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[\d\u{1F600}-\u{1F64F}]", "5", true)]
    [InlineData(@"[\d\u{1F600}-\u{1F64F}]", "\U0001F600", true)]
    [InlineData(@"[\d\u{1F600}-\u{1F64F}]", "a", false)]
    public void ShorthandWithSupplementaryInClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 72. SUPPLEMENTARY TRANSLATION FORMAT
    // ================================================================

    [Theory]
    [InlineData(@"[a-z\u{1F600}-\u{1F64F}]", @"(?:[a-z]|\uD83D[\uDE00-\uDE4F])")]
    [InlineData(@"[A\u{1F600}-\u{1F64F}Z]", @"(?:[AZ]|\uD83D[\uDE00-\uDE4F])")]
    public void MixedSupplementaryTranslationFormatIsCorrect(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ================================================================
    // 73. GETMAXTRANSLATEDLENGTH COVERS NEW PATTERNS
    // ================================================================

    [Theory]
    [InlineData(@"(a)\1")]
    [InlineData(@"(a)(b)\1\2")]
    [InlineData(@"(?<n>a)\k<n>")]
    [InlineData(@"[\u{1F600}-\u{1F64F}]")]
    [InlineData(@"[a-z\u{1F600}-\u{1F64F}\u{1F900}-\u{1F9FF}]")]
    [InlineData(@"[\u{10000}-\u{10FFFF}]")]
    public void GetMaxTranslatedLengthCoversNewPatterns(string ecma)
    {
        int maxLen = EcmaRegexTranslator.GetMaxTranslatedLength(ecma);
        string translated = EcmaRegexTranslator.Translate(ecma);
        Assert.True(maxLen >= translated.Length,
            $"GetMaxTranslatedLength returned {maxLen} but translated pattern is {translated.Length} chars: {translated}");
    }

    // ================================================================
    // 74. TRYTRANSLATE BUFFER TOO SMALL
    // ================================================================

    [Theory]
    [InlineData(@"(a)\1", 5)]
    [InlineData(@"[\u{1F600}-\u{1F64F}]", 10)]
    public void TryTranslateReturnsTooSmallForInsufficientBuffer(string ecma, int bufferSize)
    {
        Span<char> buffer = stackalloc char[bufferSize];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);
        Assert.Equal(OperationStatus.DestinationTooSmall, status);
    }

    // ================================================================
    // 75. \u{} WITH LEADING ZEROS (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"\u{00041}", @"\u0041")]
    [InlineData(@"\u{0001F600}", @"(?:\uD83D\uDE00)")]
    [InlineData(@"\u{0}", @"\u0000")]
    [InlineData(@"\u{00}", @"\u0000")]
    public void LeadingZerosInBraceEscapeTranslateCorrectly(string ecma, string expected)
    {
        Assert.Equal(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [Theory]
    [InlineData(@"\u{00041}", "A", true)]
    [InlineData(@"\u{00041}", "B", false)]
    [InlineData(@"\u{0001F600}", "\U0001F600", true)]
    [InlineData(@"\u{0001F600}", "A", false)]
    public void LeadingZerosInBraceEscapeMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 76. MIXED ESCAPE TYPES IN CHAR CLASS RANGES (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[\u0041-Z]", "A", true)]
    [InlineData(@"[\u0041-Z]", "M", true)]
    [InlineData(@"[\u0041-Z]", "Z", true)]
    [InlineData(@"[\u0041-Z]", "a", false)]
    [InlineData(@"[\x41-Z]", "A", true)]
    [InlineData(@"[\x41-Z]", "a", false)]
    [InlineData(@"[A-\u{5A}]", "A", true)]
    [InlineData(@"[A-\u{5A}]", "Z", true)]
    [InlineData(@"[A-\u{5A}]", "a", false)]
    [InlineData(@"[\x41-\u{5A}]", "M", true)]
    public void MixedEscapeTypesInCharClassRangesMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 77. SUPPLEMENTARY + LITERAL DASH (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[\u{1F600}-]", "\U0001F600", true)]
    [InlineData(@"[\u{1F600}-]", "-", true)]
    [InlineData(@"[\u{1F600}-]", "a", false)]
    public void SupplementaryWithLiteralDashMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 78. NEGATED SHORTHAND + SUPPLEMENTARY INTERACTION (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[\D\u{1F600}]", "a", true)]        // \D matches non-digit
    [InlineData(@"[\D\u{1F600}]", "\U0001F600", true)]// supplementary char
    [InlineData(@"[\D\u{1F600}]", "5", false)]        // digit excluded by \D
    [InlineData(@"[^\D\u{1F600}]", "5", true)]        // NOT(\D ∪ {emoji}) = digits
    [InlineData(@"[^\D\u{1F600}]", "a", false)]       // letter in \D
    public void NegatedShorthandWithSupplementaryMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 79. DASH AT BOUNDARIES IN CHARACTER CLASSES (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"[-a]", "-", true)]
    [InlineData(@"[-a]", "a", true)]
    [InlineData(@"[-a]", "b", false)]
    [InlineData(@"[a-]", "-", true)]
    [InlineData(@"[a-]", "a", true)]
    [InlineData(@"[a-]", "b", false)]
    public void DashAtBoundariesInCharClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 80. ALTERNATION EDGE CASES (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"|a", "", true)]
    [InlineData(@"|a", "a", true)]
    [InlineData(@"a|", "", true)]
    [InlineData(@"a|", "a", true)]
    [InlineData(@"||", "", true)]
    public void AlternationEdgeCasesMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 81. EMPTY PATTERN (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"", "", true)]
    [InlineData(@"", "abc", true)]
    public void EmptyPatternMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 82. DOT WITH QUANTIFIERS (JS-validated)
    // ================================================================

    [Theory]
    [InlineData(@"a.*b", "aXb", true)]
    [InlineData(@"a.+b", "aXb", true)]
    [InlineData(@"a.{3}b", "aXYZb", true)]
    public void DotWithQuantifiersMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 83. BINARY UNICODE PROPERTIES — translated to .NET equivalents
    // ================================================================

    [Theory]
    [InlineData(@"\p{Emoji}", "\U0001F600", true)]
    [InlineData(@"\p{Emoji}", "a", false)]
    [InlineData(@"\p{ASCII}", "A", true)]
    [InlineData(@"\p{ASCII}", "\U0001F600", false)]
    [InlineData(@"\p{Alphabetic}", "a", true)]
    [InlineData(@"\p{White_Space}", " ", true)]
    public void BinaryPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 84. SCRIPT MAPPING — Script names mapped to .NET block names
    // ================================================================

    [Theory]
    [InlineData(@"\p{Script=Latin}", "a", true)]
    [InlineData(@"\p{Script=Latin}", "\u03B1", false)]
    [InlineData(@"\p{sc=Latn}", "a", true)]
    public void ScriptLatinMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 84b. SCRIPT=LATIN EXTENDED — full Latin script coverage
    // ================================================================

    [Theory]
    [InlineData(@"\p{Script=Latin}", "\u00E9", true)]  // é — Latin-1 Supplement
    [InlineData(@"\p{Script=Latin}", "\u0100", true)]  // Ā — Latin Extended-A
    [InlineData(@"\p{Script=Latin}", "\u0180", true)]  // ƀ — Latin Extended-B
    [InlineData(@"\p{Script=Latin}", "\u1E00", true)]  // Ḁ — Latin Extended Additional
    [InlineData(@"\p{Script=Latin}", "!", false)]       // Punctuation — Common script
    [InlineData(@"\p{Script=Latin}", "0", false)]       // Digit — Common script
    [InlineData(@"\p{Script=Latin}", " ", false)]       // Space — Common script
    public void ScriptLatinExtendedMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 85. WRONG-CASE PROPERTY NAMES — invalid in ES /u mode, rejected
    // ================================================================

    [Theory]
    [InlineData(@"\p{letter}")]
    [InlineData(@"\p{LETTER}")]
    public void WrongCasePropertyNameIsRejected(string ecma)
    {
        Span<char> buffer = stackalloc char[256];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);
        Assert.Equal(OperationStatus.InvalidData, status);
    }

    // ================================================================
    // 86. BINARY PROPERTIES — INSIDE CHARACTER CLASSES
    // BMP-only binary properties can be inlined; supplementary ones cannot.
    // ================================================================

    [Theory]
    [InlineData(@"[\p{ASCII}]+", "Hello", true)]
    [InlineData(@"[\p{ASCII}]+", "\u00FF", false)]
    [InlineData(@"[\p{ASCII}\p{L}]+", "Hélló", true)]
    [InlineData(@"[\p{White_Space}]+", " \t\n", true)]
    [InlineData(@"[\p{White_Space}]+", "abc", false)]
    [InlineData(@"[\p{Alphabetic}0-9]+", "abc123", true)]
    [InlineData(@"[\p{Alphabetic}0-9]+", "!!!", false)]
    public void BinaryPropertyInsideClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 87. NEGATED BINARY PROPERTIES (standalone)
    // ================================================================

    [Theory]
    [InlineData(@"\P{ASCII}", "\u00FF", true)]
    [InlineData(@"\P{ASCII}", "A", false)]
    [InlineData(@"\P{Alphabetic}", "!", true)]
    [InlineData(@"\P{Alphabetic}", "a", false)]
    [InlineData(@"\P{White_Space}", "x", true)]
    [InlineData(@"\P{White_Space}", " ", false)]
    [InlineData(@"\P{Emoji}", "x", true)]
    [InlineData(@"\P{Emoji}", "\U0001F600", false)]
    public void NegatedBinaryPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 88. MORE SCRIPT MAPPINGS
    // ================================================================

    [Theory]
    [InlineData(@"\p{Script=Greek}", "\u03B1", true)]
    [InlineData(@"\p{Script=Greek}", "a", false)]
    [InlineData(@"\p{sc=Grek}", "\u03B1", true)]
    [InlineData(@"\p{Script=Cyrillic}", "\u0414", true)]
    [InlineData(@"\p{Script=Cyrillic}", "a", false)]
    [InlineData(@"\p{sc=Cyrl}", "\u0414", true)]
    [InlineData(@"\p{Script=Arabic}", "\u0627", true)]
    [InlineData(@"\p{Script=Arabic}", "a", false)]
    [InlineData(@"\p{Script=Hebrew}", "\u05D0", true)]
    [InlineData(@"\p{Script=Hebrew}", "a", false)]
    [InlineData(@"\p{Script=Devanagari}", "\u0905", true)]
    [InlineData(@"\p{Script=Devanagari}", "a", false)]
    [InlineData(@"\p{Script=Thai}", "\u0E01", true)]
    [InlineData(@"\p{Script=Thai}", "a", false)]
    public void ScriptPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 89. EMOJI EDGE CASES
    // ================================================================

    [Theory]
    [InlineData(@"\p{Emoji}", "\u2764", true)]   // ❤ Heavy black heart
    [InlineData(@"\p{Emoji}", "\u2600", true)]   // ☀ Sun
    [InlineData(@"\p{Emoji}", "\u00A9", true)]   // © Copyright
    [InlineData(@"\p{Emoji}", "\u00AE", true)]   // ® Registered
    [InlineData(@"\p{Emoji}", "\u0023", true)]   // # (Emoji property includes digits, #, *)
    [InlineData(@"\p{Emoji}", "\u002A", true)]   // *
    [InlineData(@"\p{Emoji}", "0", true)]        // digit 0
    [InlineData(@"\p{Emoji}", "\U0001F601", true)]  // 😁
    [InlineData(@"\p{Emoji}", "\U0001F9FF", true)]  // last in U+1F900-1FFFF range
    [InlineData(@"\p{Emoji}", "Z", false)]
    public void EmojiEdgeCasesMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 90. UNKNOWN/INVALID PROPERTY NAMES — rejected in /u mode
    // ================================================================

    [Theory]
    [InlineData(@"\p{Bogus}")]
    [InlineData(@"\p{NotAProp}")]
    [InlineData(@"\p{Script=Klingon}")]
    [InlineData(@"\p{gc=XX}")]
    [InlineData(@"\p{foo=bar}")]
    public void UnknownPropertyNameIsRejected(string ecma)
    {
        Span<char> buffer = stackalloc char[256];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);
        Assert.Equal(OperationStatus.InvalidData, status);
    }

    // ================================================================
    // 91. BINARY \p{Any} AND \p{Assigned}
    // ================================================================

    [Theory]
    [InlineData(@"\p{Any}", "a", true)]
    [InlineData(@"\p{Any}", "\U0001F600", true)]
    [InlineData(@"\p{Assigned}", "a", true)]
    [InlineData(@"\P{Assigned}", "a", false)]
    public void AnyAndAssignedPropertiesMatch(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 92. NEGATED SCRIPT PROPERTIES
    // ================================================================

    [Theory]
    [InlineData(@"\P{Script=Latin}", "\u03B1", true)]   // Greek α — not Latin
    [InlineData(@"\P{Script=Latin}", "a", false)]        // Latin a
    [InlineData(@"\P{sc=Greek}", "a", true)]             // a — not Greek
    [InlineData(@"\P{sc=Greek}", "\u03B1", false)]       // Greek α
    [InlineData(@"\P{Script=Cyrillic}", "a", true)]
    [InlineData(@"\P{Script=Cyrillic}", "\u0414", false)]
    public void NegatedScriptPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 93. ALTERNATION-PATTERN PROPERTIES INSIDE CHARACTER CLASSES
    // ================================================================

    [Theory]
    [InlineData(@"[\p{Emoji}]", "\U0001F600", true)]
    [InlineData(@"[\p{Emoji}]", "Z", false)]
    [InlineData(@"[\p{Emoji}a-z]", "a", true)]
    [InlineData(@"[\p{Emoji}a-z]", "\U0001F600", true)]
    [InlineData(@"[\p{Emoji}a-z]", "Z", false)]
    [InlineData(@"[\p{Script=Latin}]", "a", true)]
    [InlineData(@"[\p{Script=Latin}]", "\u00E9", true)]
    [InlineData(@"[\p{Script=Latin}]", "\u03B1", false)]
    [InlineData(@"[\p{Script=Latin}0-9]", "5", true)]
    [InlineData(@"[\p{Script=Latin}0-9]", "\u03B1", false)]
    public void AlternationPropertyInCharClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 94. NEGATED CLASS WITH ALTERNATION-PATTERN PROPERTY
    // ================================================================

    [Theory]
    [InlineData(@"[^\p{Emoji}]", "Z", true)]
    [InlineData(@"[^\p{Emoji}]", "\U0001F600", false)]
    [InlineData(@"[^\p{Script=Latin}]", "\u03B1", true)]
    [InlineData(@"[^\p{Script=Latin}]", "a", false)]
    public void NegatedClassWithAlternationPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 95. MULTIPLE ALTERNATION PROPERTIES IN ONE CLASS
    // ================================================================

    [Theory]
    [InlineData(@"[\p{Emoji}\p{Script=Latin}]", "\U0001F600", true)]
    [InlineData(@"[\p{Emoji}\p{Script=Latin}]", "a", true)]
    [InlineData(@"[\p{Emoji}\p{Script=Latin}]", "\u03B1", false)]
    public void MultipleAlternationPropertiesInClassMatch(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 96. DOUBLE NEGATION — [^\P{L}] = [\p{L}]
    // ================================================================

    [Theory]
    [InlineData(@"[^\P{L}]", "a", true)]
    [InlineData(@"[^\P{L}]", "0", false)]
    public void DoubleNegationPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 97. MALFORMED PROPERTY NAMES — rejected as InvalidData
    // ================================================================

    [Theory]
    [InlineData(@"\p{}")]
    [InlineData(@"\p{=}")]
    [InlineData(@"\p{gc=}")]
    [InlineData(@"\p{=Latin}")]
    public void MalformedPropertyNameIsRejected(string ecma)
    {
        Span<char> buffer = stackalloc char[256];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);
        Assert.Equal(OperationStatus.InvalidData, status);
    }

    // ================================================================
    // 98. ADDITIONAL BINARY PROPERTIES
    // ================================================================

    [Theory]
    [InlineData(@"\p{Hex_Digit}", "A", true)]
    [InlineData(@"\p{Hex_Digit}", "f", true)]
    [InlineData(@"\p{Hex_Digit}", "9", true)]
    [InlineData(@"\p{Hex_Digit}", "\uFF21", true)]   // Fullwidth A
    [InlineData(@"\p{Hex_Digit}", "\uFF46", true)]   // Fullwidth f
    [InlineData(@"\p{Hex_Digit}", "g", false)]
    [InlineData(@"\p{Hex_Digit}", "G", false)]
    [InlineData(@"\P{Hex_Digit}", "g", true)]
    [InlineData(@"\P{Hex_Digit}", "A", false)]
    public void HexDigitPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"\p{Lowercase}", "a", true)]
    [InlineData(@"\p{Lowercase}", "z", true)]
    [InlineData(@"\p{Lowercase}", "\u00AA", true)]    // Feminine ordinal (Other_Lowercase)
    [InlineData(@"\p{Lowercase}", "\u00BA", true)]    // Masculine ordinal (Other_Lowercase)
    [InlineData(@"\p{Lowercase}", "\u02B0", true)]    // Modifier letter small h (Other_Lowercase)
    [InlineData(@"\p{Lowercase}", "\u2170", true)]    // Small Roman numeral I (Other_Lowercase)
    [InlineData(@"\p{Lowercase}", "A", false)]
    [InlineData(@"\p{Lowercase}", "Z", false)]
    [InlineData(@"\p{Lowercase}", "0", false)]
    [InlineData(@"\P{Lowercase}", "A", true)]
    [InlineData(@"\P{Lowercase}", "a", false)]
    public void LowercasePropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"\p{Uppercase}", "A", true)]
    [InlineData(@"\p{Uppercase}", "Z", true)]
    [InlineData(@"\p{Uppercase}", "\u2160", true)]    // Roman numeral I (Other_Uppercase)
    [InlineData(@"\p{Uppercase}", "\u24B6", true)]    // Circled Latin capital A (Other_Uppercase)
    [InlineData(@"\p{Uppercase}", "a", false)]
    [InlineData(@"\p{Uppercase}", "z", false)]
    [InlineData(@"\p{Uppercase}", "0", false)]
    [InlineData(@"\P{Uppercase}", "a", true)]
    [InlineData(@"\P{Uppercase}", "A", false)]
    public void UppercasePropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"\p{ID_Start}", "a", true)]
    [InlineData(@"\p{ID_Start}", "Z", true)]
    [InlineData(@"\p{ID_Start}", "\u00C0", true)]     // Latin capital A with grave (L category)
    [InlineData(@"\p{ID_Start}", "\u2118", true)]     // Weierstrass P (Other_ID_Start)
    [InlineData(@"\p{ID_Start}", "\u212E", true)]     // Estimated symbol (Other_ID_Start)
    [InlineData(@"\p{ID_Start}", "\u1885", true)]     // Mongolian letter Ali Gali (Other_ID_Start)
    [InlineData(@"\p{ID_Start}", "0", false)]
    [InlineData(@"\p{ID_Start}", "_", false)]
    [InlineData(@"\p{ID_Start}", " ", false)]
    [InlineData(@"\P{ID_Start}", "0", true)]
    [InlineData(@"\P{ID_Start}", "a", false)]
    public void IdStartPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"\p{Emoji_Presentation}", "\u231A", true)]    // Watch
    [InlineData(@"\p{Emoji_Presentation}", "\u2614", true)]    // Umbrella with rain
    [InlineData(@"\p{Emoji_Presentation}", "\U0001F600", true)] // Grinning face
    [InlineData(@"\p{Emoji_Presentation}", "\U0001F4A9", true)] // Pile of poo
    [InlineData(@"\p{Emoji_Presentation}", "a", false)]
    [InlineData(@"\p{Emoji_Presentation}", "0", false)]
    [InlineData(@"\P{Emoji_Presentation}", "a", true)]
    [InlineData(@"\P{Emoji_Presentation}", "\u231A", false)]
    public void EmojiPresentationPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [Theory]
    [InlineData(@"\p{Extended_Pictographic}", "\u00A9", true)]  // Copyright sign
    [InlineData(@"\p{Extended_Pictographic}", "\u00AE", true)]  // Registered sign
    [InlineData(@"\p{Extended_Pictographic}", "\u2728", true)]  // Sparkles
    [InlineData(@"\p{Extended_Pictographic}", "\U0001F600", true)] // Grinning face
    [InlineData(@"\p{Extended_Pictographic}", "\U0001F9FF", true)] // Nazar amulet
    [InlineData(@"\p{Extended_Pictographic}", "a", false)]
    [InlineData(@"\p{Extended_Pictographic}", "0", false)]
    [InlineData(@"\P{Extended_Pictographic}", "a", true)]
    [InlineData(@"\P{Extended_Pictographic}", "\u00A9", false)]
    public void ExtendedPictographicPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 99. LOOKAROUND WITH PROPERTY ESCAPES
    // ================================================================

    [Theory]
    [InlineData(@"(?=\p{L}).", "abc", true)]
    [InlineData(@"(?=\p{L}).", "123", false)]
    [InlineData(@"(?<=\p{L})\d", "a5", true)]
    [InlineData(@"(?<=\p{L})\d", " 5", false)]
    [InlineData(@"(?!\p{Emoji}).", "a", true)]
    public void LookaroundWithPropertyEscapeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 100. QUANTIFIERS ON EXPANDED PROPERTIES
    // ================================================================

    [Theory]
    [InlineData(@"\p{Script=Latin}+", "hello", true)]
    [InlineData(@"\p{Script=Latin}+", "\u03B1", false)]
    [InlineData(@"\p{Emoji}?x", "x", true)]
    [InlineData(@"\p{Emoji}?x", "\U0001F600x", true)]
    [InlineData(@"\p{Emoji}{2}", "\U0001F600\U0001F601", true)]
    [InlineData(@"\p{Emoji}{2}", "\U0001F600", false)]
    public void QuantifiersOnExpandedPropertiesMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 101. GetMaxTranslatedLength COVERS LARGE SCRIPT PATTERNS
    // ================================================================

    [Fact]
    public void GetMaxTranslatedLengthCoversLatinScript()
    {
        string ecma = @"\p{Script=Latin}";
        int maxLen = EcmaRegexTranslator.GetMaxTranslatedLength(ecma);
        string result = EcmaRegexTranslator.Translate(ecma);
        Assert.True(maxLen >= result.Length,
            $"MaxLen {maxLen} < actual {result.Length}");
    }

    // ================================================================
    // Helper
    // ================================================================

    private static void AssertMatch(string ecmaPattern, string input, bool shouldMatch)
    {
        string dotNetPattern = EcmaRegexTranslator.Translate(ecmaPattern);
        Regex regex = new(dotNetPattern);
        bool isMatch = regex.IsMatch(input);
        Assert.Equal(shouldMatch, isMatch);
    }
}
