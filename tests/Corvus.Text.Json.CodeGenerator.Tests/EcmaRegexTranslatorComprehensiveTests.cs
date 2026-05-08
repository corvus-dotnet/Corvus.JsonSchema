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
[TestClass]
public class EcmaRegexTranslatorComprehensiveTests
{
    // ================================================================
    // 1. ALL WHITESPACE CHARACTERS — individual Zs category members
    // ================================================================

    [TestMethod]
    [DataRow(@"\s", "\u1680", true)]   // Ogham Space Mark
    [DataRow(@"\s", "\u2000", true)]   // En Quad
    [DataRow(@"\s", "\u2001", true)]   // Em Quad
    [DataRow(@"\s", "\u2002", true)]   // En Space
    [DataRow(@"\s", "\u2004", true)]   // Three-Per-Em Space
    [DataRow(@"\s", "\u2005", true)]   // Four-Per-Em Space
    [DataRow(@"\s", "\u2006", true)]   // Six-Per-Em Space
    [DataRow(@"\s", "\u2007", true)]   // Figure Space
    [DataRow(@"\s", "\u2008", true)]   // Punctuation Space
    [DataRow(@"\s", "\u2009", true)]   // Thin Space
    [DataRow(@"\s", "\u200A", true)]   // Hair Space
    [DataRow(@"\s", "\u202F", true)]   // Narrow No-Break Space
    [DataRow(@"\s", "\u205F", true)]   // Medium Mathematical Space
    public void AllZsCategoryMembersMatchWhitespace(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 2. WORD BOUNDARY (\b) — edge cases
    // ================================================================

    [TestMethod]
    [DataRow(@"\ba", "abc", true)]      // \b at string start before word char
    [DataRow(@"c\b", "abc", true)]      // \b at string end after word char
    [DataRow(@"\b", "a", true)]         // \b matches at start of single word char
    [DataRow(@"\b", " ", false)]        // no word chars → no boundary
    [DataRow(@"^\b", "abc", true)]      // \b at start of string
    [DataRow(@"\b$", "abc", true)]      // \b at end of string
    public void WordBoundaryEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"\bé", "café", true)]     // \b between 'f'(word) and 'é'(non-word) — JS verified
    public void WordBoundaryWithUnicodeChars(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 3. NON-WORD BOUNDARY (\B) — edge cases
    // ================================================================

    [TestMethod]
    [DataRow(@"\Ba", "ba", true)]       // \B between two word chars
    [DataRow(@"\B.", " a", true)]       // \B between two non-word chars (space + before a, at pos 0)
    public void NonWordBoundaryEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 4. CHARACTER CLASSES WITH \S NEGATED SHORTHAND
    // ================================================================

    [TestMethod]
    [DataRow(@"[\S]", "a", true)]
    [DataRow(@"[\S]", "0", true)]
    [DataRow(@"[\S]", " ", false)]
    [DataRow(@"[\S]", "\t", false)]
    public void CharClassWithNegatedS(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[^\S]", " ", true)]
    [DataRow(@"[^\S]", "\t", true)]
    [DataRow(@"[^\S]", "a", false)]
    public void NegatedCharClassWithNegatedS(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[a\S]", "a", true)]
    [DataRow(@"[a\S]", "z", true)]
    [DataRow(@"[a\S]", " ", false)]
    public void CharClassWithPositiveAndNegatedS(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 5. MIXED NEGATED SHORTHAND COMBINATIONS IN CLASSES
    // ================================================================

    [TestMethod]
    [DataRow(@"[\D\S]", "a", true)]   // \D ∪ \S = everything
    [DataRow(@"[\D\S]", "0", true)]
    [DataRow(@"[\D\S]", " ", true)]
    [DataRow(@"[\D\S]", "\t", true)]
    public void CharClassDSMatchesEverything(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[^\D\S]", "a", false)]   // NOT(\D ∪ \S) = nothing
    [DataRow(@"[^\D\S]", "0", false)]
    [DataRow(@"[^\D\S]", " ", false)]
    public void NegatedCharClassDSMatchesNothing(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[\W\S]", "a", true)]   // \W ∪ \S = everything
    [DataRow(@"[\W\S]", " ", true)]
    [DataRow(@"[\W\S]", "0", true)]
    public void CharClassWSMatchesEverything(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[^\W\S]", "a", false)]   // NOT(\W ∪ \S) = nothing
    [DataRow(@"[^\W\S]", " ", false)]
    [DataRow(@"[^\W\S]", "0", false)]
    public void NegatedCharClassWSMatchesNothing(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[\D\W]", "a", true)]     // letters are non-digits
    [DataRow(@"[\D\W]", " ", true)]      // space is non-digit and non-word
    [DataRow(@"[\D\W]", "_", true)]      // underscore is non-digit (in \D)
    [DataRow(@"[\D\W]", "0", false)]     // digit AND word char → not in \D, not in \W
    public void CharClassDWMatchesNonDigitsOrNonWord(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[^\D\W]", "0", true)]    // NOT(\D ∪ \W) = digits (because digits are in \w and not in \D)
    [DataRow(@"[^\D\W]", "5", true)]
    [DataRow(@"[^\D\W]", "a", false)]   // letter is in \D
    [DataRow(@"[^\D\W]", " ", false)]
    [DataRow(@"[^\D\W]", "_", false)]   // underscore is in \D
    public void NegatedCharClassDWMatchesOnlyDigits(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 6. [\d\D], [\s\S], [\w\W] — match-any patterns
    // ================================================================

    [TestMethod]
    [DataRow(@"[\d\D]", "5", true)]
    [DataRow(@"[\d\D]", "a", true)]
    [DataRow(@"[\d\D]", " ", true)]
    [DataRow(@"[\d\D]", "\n", true)]
    public void DigitOrNotDigitMatchesAnything(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[\s\S]", "a", true)]
    [DataRow(@"[\s\S]", " ", true)]
    [DataRow(@"[\s\S]", "\n", true)]
    public void WhitespaceOrNotWhitespaceMatchesAnything(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[\w\W]", "a", true)]
    [DataRow(@"[\w\W]", " ", true)]
    [DataRow(@"[\w\W]", "\n", true)]
    public void WordOrNotWordMatchesAnything(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 7. CHARACTER CLASS RANGES WITH ESCAPES
    // ================================================================

    [TestMethod]
    [DataRow(@"[\u0041-\u005A]", "A", true)]
    [DataRow(@"[\u0041-\u005A]", "Z", true)]
    [DataRow(@"[\u0041-\u005A]", "M", true)]
    [DataRow(@"[\u0041-\u005A]", "a", false)]
    public void CharClassRangeWithUnicodeEscapes(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[\u{41}-\u{5A}]", "A", true)]
    [DataRow(@"[\u{41}-\u{5A}]", "Z", true)]
    [DataRow(@"[\u{41}-\u{5A}]", "a", false)]
    public void CharClassRangeWithBraceUnicodeEscapes(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[\x41-\x5A]", "A", true)]
    [DataRow(@"[\x41-\x5A]", "Z", true)]
    [DataRow(@"[\x41-\x5A]", "a", false)]
    public void CharClassRangeWithHexEscapes(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 8. HYPHEN POSITIONING IN CHARACTER CLASSES
    // ================================================================

    [TestMethod]
    [DataRow("[-abc]", "-", true)]
    [DataRow("[-abc]", "a", true)]
    [DataRow("[-abc]", "d", false)]
    [DataRow("[abc-]", "-", true)]
    [DataRow("[abc-]", "a", true)]
    [DataRow("[abc-]", "d", false)]
    [DataRow("[-]", "-", true)]
    [DataRow("[-]", "a", false)]
    public void HyphenPositioning(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 9. ESCAPED CHARACTERS INSIDE CHARACTER CLASSES
    // ================================================================

    [TestMethod]
    [DataRow(@"[\]]", "]", true)]
    [DataRow(@"[\]]", "a", false)]
    [DataRow(@"[\[]", "[", true)]
    [DataRow(@"[\[]", "a", false)]
    [DataRow(@"[\\]", "\\", true)]
    [DataRow(@"[\\]", "a", false)]
    [DataRow(@"[\-]", "-", true)]
    [DataRow(@"[\-]", "a", false)]
    public void EscapedCharsInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 10. BACKSPACE IN CHARACTER CLASS ([\b])
    // ================================================================

    [TestMethod]
    [DataRow(@"[\b]", "\u0008", true)]  // backspace character
    [DataRow(@"[\b]", "a", false)]
    public void BackspaceInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 11. UNICODE PROPERTY ESCAPES IN CHARACTER CLASSES
    // ================================================================

    [TestMethod]
    [DataRow(@"[\P{L}]", "0", true)]
    [DataRow(@"[\P{L}]", " ", true)]
    [DataRow(@"[\P{L}]", "a", false)]
    public void NegatedPropertyInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[\p{L}\d]", "a", true)]
    [DataRow(@"[\p{L}\d]", "5", true)]
    [DataRow(@"[\p{L}\d]", " ", false)]
    public void PropertyAndShorthandInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[\p{Lu}a-z]", "A", true)]
    [DataRow(@"[\p{Lu}a-z]", "a", true)]
    [DataRow(@"[\p{Lu}a-z]", "5", false)]
    public void PropertyAndRangeInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"[^\p{L}]", "0", true)]
    [DataRow(@"[^\p{L}]", "a", false)]
    public void NegatedClassWithProperty(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 12. MULTIPLE RANGES IN CHARACTER CLASSES
    // ================================================================

    [TestMethod]
    [DataRow("[a-zA-Z0-9]", "a", true)]
    [DataRow("[a-zA-Z0-9]", "Z", true)]
    [DataRow("[a-zA-Z0-9]", "5", true)]
    [DataRow("[a-zA-Z0-9]", "_", false)]
    [DataRow("[a-zA-Z0-9]", " ", false)]
    public void MultipleRangesInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 13. NESTED GROUPS
    // ================================================================

    [TestMethod]
    [DataRow("((a)(b))", "ab", true)]
    [DataRow("((a)(b))", "ac", false)]
    [DataRow("((?:a)b)", "ab", true)]
    [DataRow("((?:a)b)", "cb", false)]
    public void NestedGroups(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 14. ALTERNATION INSIDE GROUPS
    // ================================================================

    [TestMethod]
    [DataRow("(a|b|c)", "a", true)]
    [DataRow("(a|b|c)", "b", true)]
    [DataRow("(a|b|c)", "c", true)]
    [DataRow("(a|b|c)", "d", false)]
    [DataRow("(?:a|b|c)", "a", true)]
    [DataRow("(a|bb|ccc)", "bb", true)]
    [DataRow("(a|bb|ccc)", "dd", false)]
    public void AlternationInsideGroups(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow("(a|(b|c))", "a", true)]
    [DataRow("(a|(b|c))", "b", true)]
    [DataRow("(a|(b|c))", "c", true)]
    [DataRow("(a|(b|c))", "d", false)]
    public void NestedAlternation(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 15. EMPTY ALTERNATION
    // ================================================================

    [TestMethod]
    [DataRow("a|", "a", true)]
    [DataRow("a|", "", true)]       // empty alternative matches empty string
    [DataRow("a|", "b", true)]      // empty alternative matches at start of 'b'
    [DataRow("|b", "b", true)]
    [DataRow("|b", "", true)]
    [DataRow("a||b", "", true)]
    [DataRow("a||b", "a", true)]
    [DataRow("a||b", "b", true)]
    public void EmptyAlternation(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 16. ALTERNATION SCOPE (precedence)
    // ================================================================

    [TestMethod]
    [DataRow("^a|b$", "a", true)]     // (^a) | (b$) — not ^(a|b)$
    [DataRow("^a|b$", "b", true)]
    [DataRow("^a|b$", "xb", true)]    // b$ matches
    [DataRow("^a|b$", "ax", true)]    // ^a matches
    public void AlternationScope(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 17. QUANTIFIER EDGE CASES
    // ================================================================

    [TestMethod]
    [DataRow("^a{0}$", "", true)]
    [DataRow("^a{0}$", "a", false)]
    [DataRow("^a{0,0}$", "", true)]
    [DataRow("^a{0,0}$", "a", false)]
    [DataRow("^a{0,1}$", "", true)]
    [DataRow("^a{0,1}$", "a", true)]
    [DataRow("^a{0,1}$", "aa", false)]
    [DataRow("a{0,}", "", true)]
    [DataRow("^a{3,5}$", "aaaaaa", false)]
    public void QuantifierEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow("(ab)+", "ababab", true)]
    [DataRow("(ab)+", "cd", false)]
    [DataRow("(?:ab){2}", "abab", true)]
    [DataRow("(?:ab){2}", "ab", false)]
    [DataRow("(a|b)+", "aabba", true)]
    [DataRow("(a|b)+", "cccc", false)]
    public void QuantifierOnGroups(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow("[a-z]+", "abc", true)]
    [DataRow("[a-z]+", "123", false)]
    [DataRow("[a-z]{2,4}", "ab", true)]
    [DataRow("^[a-z]{2,4}$", "abcde", false)]
    public void QuantifierOnCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // Lazy quantifiers
    [TestMethod]
    [DataRow("a*?", "", true)]
    [DataRow("a+?", "a", true)]
    [DataRow("a+?", "", false)]
    [DataRow("a{3,5}?", "aaa", true)]
    public void LazyQuantifiers(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 18. BACKREFERENCE EDGE CASES
    // ================================================================

    [TestMethod]
    [DataRow(@"(a)\1", "aa", true)]
    [DataRow(@"(a)\1", "ab", false)]
    [DataRow(@"(a)(b)\1\2", "abab", true)]
    [DataRow(@"(a)(b)\1\2", "abba", false)]
    public void MultipleBackreferences(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10", "abcdefghijj", true)]
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10", "abcdefghijk", false)]
    public void MultiDigitBackreferences(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 19. ASSERTION EDGE CASES
    // ================================================================

    [TestMethod]
    [DataRow("^$", "", true)]
    [DataRow("^$", "a", false)]
    public void EmptyStringAnchors(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow("q(?!u)", "qi", true)]
    [DataRow("q(?!u)", "qu", false)]
    public void NegativeLookahead(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow("(?<!a)b", "cb", true)]
    [DataRow("(?<!a)b", "ab", false)]
    public void NegativeLookbehind(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow("(?=a(?=b))", "ab", true)]
    [DataRow("(?=a(?=b))", "ac", false)]
    public void NestedLookaround(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow("a(?=)b", "ab", true)]     // empty positive lookahead always succeeds
    [DataRow("a(?!)b", "ab", false)]    // empty negative lookahead always fails
    public void EmptyLookaround(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"(?=.*\d)", "abc1", true)]
    [DataRow(@"(?=.*\d)", "abcd", false)]
    public void LookaheadWithShorthand(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 20. UNICODE ESCAPE EDGE CASES
    // ================================================================

    [TestMethod]
    [DataRow(@"\u{0}", "\0", true)]
    [DataRow(@"\u{0}", "A", false)]
    [DataRow(@"\u{FF}", "\u00FF", true)]
    [DataRow(@"\u{FFFF}", "\uFFFF", true)]
    [DataRow(@"\u{10000}", "\U00010000", true)]
    [DataRow(@"\u{10FFFF}", "\U0010FFFF", true)]
    public void UnicodeEscapeEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 21. CONTROL ESCAPE EDGE CASES
    // ================================================================

    [TestMethod]
    [DataRow(@"\cJ", "\n", true)]       // \cJ = LF
    [DataRow(@"\cI", "\t", true)]       // \cI = TAB
    [DataRow(@"\cA", "\u0001", true)]   // \cA = SOH
    [DataRow(@"\cZ", "\u001A", true)]   // \cZ = SUB
    public void ControlEscapeEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 22. HEX ESCAPE EDGE CASES
    // ================================================================

    [TestMethod]
    [DataRow(@"\x61", "a", true)]
    [DataRow(@"\x20", " ", true)]
    [DataRow(@"\x00", "\0", true)]
    public void HexEscapeEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 23. DOT WITH QUANTIFIERS
    // ================================================================

    [TestMethod]
    [DataRow(".+", "abc", true)]
    [DataRow(".+", "", false)]
    [DataRow(".*", "", true)]
    [DataRow(".{3}", "abc", true)]
    [DataRow("^.{3}$", "ab", false)]
    public void DotWithQuantifiers(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 24. ES2025 MODIFIER GROUPS (?i:...)
    // ================================================================

    [TestMethod]
    [DataRow("(?i:abc)", "ABC", true)]
    [DataRow("(?i:abc)", "abc", true)]
    [DataRow("(?i:abc)", "AbC", true)]
    [DataRow("(?i:abc)def", "ABCdef", true)]
    [DataRow("(?i:abc)def", "ABCDEF", false)]  // only group is case-insensitive
    [DataRow("a(?i:bc)d", "aBCd", true)]
    [DataRow("a(?i:bc)d", "ABCD", false)]      // 'a' and 'd' must match exactly
    public void ModifierGroups(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 25. CONSECUTIVE CHARACTER CLASSES
    // ================================================================

    [TestMethod]
    [DataRow("[a-z][0-9]", "a5", true)]
    [DataRow("[a-z][0-9]", "5a", false)]
    public void ConsecutiveCharClasses(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 26. MULTIPLE ESCAPES IN SEQUENCE
    // ================================================================

    [TestMethod]
    [DataRow(@"^\d\w\s$", "1a ", true)]
    [DataRow(@"^\d\w\s$", "aaa", false)]
    public void MultipleEscapesInSequence(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 27. COMBINED FEATURES — word boundary + shorthands
    // ================================================================

    [TestMethod]
    [DataRow(@"\b\w+\b\s+\w+", "hello world", true)]
    [DataRow(@"\b\w+\b\s+\w+", "hello", false)]
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

    [TestMethod]
    [DataRow(@"\p{sc=Greek}", "\u03B1", true)]         // α (alpha) — IsGreek block
    [DataRow(@"\p{sc=Greek}", "a", false)]
    [DataRow(@"\p{Script_Extensions=Cyrillic}", "\u0410", true)]  // IsCyrillic block
    [DataRow(@"\p{Script_Extensions=Cyrillic}", "a", false)]
    public void PropertyScriptVariantsWithBlockNames(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 29. UNICODE PROPERTY — gc= variants
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{gc=Nd}", "3", true)]
    [DataRow(@"\p{gc=Nd}", "a", false)]
    public void PropertyGcVariants(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 30. TRANSLATION TESTS — new patterns
    // ================================================================

    [TestMethod]
    [DataRow(@"[\S]", @"[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    [DataRow(@"[^\S]", @"[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    public void NegatedSShorthandInCharClassTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"[a\S]", @"(?:[a]|[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000])")]
    public void MixedPositiveAndNegatedSTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"[\D\S]", @"(?:[^0-9]|[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000])")]
    [DataRow(@"[\D\W]", "(?:[^0-9]|[^a-zA-Z0-9_])")]
    [DataRow(@"[\W\S]", @"(?:[^a-zA-Z0-9_]|[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000])")]
    public void MultipleNegatedShorthandsNonNegatedClassTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"[^\D\S]", @"[^\s\S]")]       // empty: D+S in negated
    [DataRow(@"[^\W\S]", @"[^\s\S]")]       // empty: W+S in negated
    [DataRow(@"[^\D\W]", "[0-9]")]           // intersection of [0-9] and [a-zA-Z0-9_]
    public void MultipleNegatedShorthandsNegatedClassTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"[\d\D]", "(?:[0-9]|[^0-9])")]
    [DataRow(@"[\s\S]", @"(?:[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]|[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000])")]
    [DataRow(@"[\w\W]", "(?:[a-zA-Z0-9_]|[^a-zA-Z0-9_])")]
    public void ComplementPairsInClassTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"[\P{L}]", @"[\P{L}]")]
    [DataRow(@"[\p{L}\d]", @"[\p{L}0-9]")]
    [DataRow(@"[\p{Lu}a-z]", @"[\p{Lu}a-z]")]
    [DataRow(@"[^\p{L}]", @"[^\p{L}]")]
    public void PropertyEscapesInCharClassTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"\u{0}", @"\u0000")]
    [DataRow(@"\u{A}", @"\u000A")]
    [DataRow(@"\u{FF}", @"\u00FF")]
    [DataRow(@"\u{100}", @"\u0100")]
    [DataRow(@"\u{10000}", @"(?:\uD800\uDC00)")]
    public void UnicodeEscapeEdgeCasesTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow("(?i:abc)", "(?i:abc)")]
    [DataRow("a(?i:bc)d", "a(?i:bc)d")]
    [DataRow("(?i:abc)def", "(?i:abc)def")]
    public void ModifierGroupsPassThroughTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow("a|", "a|")]
    [DataRow("|b", "|b")]
    [DataRow("a||b", "a||b")]
    public void EmptyAlternationPassThroughTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"(a)(b)\1\2", @"(a)(b)(?(1)\1)(?(2)\2)")]
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10", @"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(?(10)\10)")]
    public void MultiDigitBackrefTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow("((a)(b))", "((a)(b))")]
    [DataRow("((?:a)b)", "((?:a)b)")]
    [DataRow("(a|(b|c))", "(a|(b|c))")]
    public void NestedGroupsPassThroughTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow("a{0}", "a{0}")]
    [DataRow("a{0,0}", "a{0,0}")]
    [DataRow("a{0,1}", "a{0,1}")]
    [DataRow("a{0,}", "a{0,}")]
    public void QuantifierEdgeCasesPassThroughTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow("[-abc]", "[-abc]")]
    [DataRow("[abc-]", "[abc-]")]
    [DataRow("[-]", "[-]")]
    public void HyphenPositionPassThroughTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"[\]]", @"[\]]")]
    [DataRow(@"[\[]", @"[\[]")]
    [DataRow(@"[\\]", @"[\\]")]
    [DataRow(@"[\-]", @"[\-]")]
    public void EscapedCharsInClassPassThroughTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow("(?=a(?=b))", "(?=a(?=b))")]
    [DataRow("(?=a(?!b))", "(?=a(?!b))")]
    [DataRow("a(?=)b", "a(?=)b")]
    [DataRow("a(?!)b", "a(?!)b")]
    public void NestedLookaroundPassThroughTranslation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ================================================================
    // 31. SPAN API — TryTranslate with comprehensive patterns
    // ================================================================

    [TestMethod]
    public void TryTranslateCharClassWithNegatedShorthand()
    {
        ReadOnlySpan<char> ecma = @"[\S]";
        Span<char> buffer = stackalloc char[256];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out int written);

        Assert.AreEqual(OperationStatus.Done, status);
        string result = new(buffer[..written]);
        StringAssert.Contains(result, @"\t\n\v\f\r");
    }

    [TestMethod]
    public void TryTranslateModifierGroup()
    {
        ReadOnlySpan<char> ecma = "(?i:abc)";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out int written);

        Assert.AreEqual(OperationStatus.Done, status);
        Assert.AreEqual("(?i:abc)", new string(buffer[..written]));
    }

    [TestMethod]
    public void TryTranslateMultiDigitBackref()
    {
        ReadOnlySpan<char> ecma = @"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10";
        Span<char> buffer = stackalloc char[256];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out int written);

        Assert.AreEqual(OperationStatus.Done, status);
        Assert.AreEqual(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(?(10)\10)", new string(buffer[..written]));
    }

    [TestMethod]
    public void GetMaxTranslatedLengthHandlesComplexPattern()
    {
        string ecma = @"[\D\S\w]+\b\p{Letter}\u{1F600}";
        int maxLen = EcmaRegexTranslator.GetMaxTranslatedLength(ecma);
        string result = EcmaRegexTranslator.Translate(ecma);

        Assert.IsTrue(maxLen >= result.Length, $"MaxLen {maxLen} < actual {result.Length}");
    }

    // ================================================================
    // 32. ERROR CASES — invalid input
    // ================================================================

    [TestMethod]
    public void InvalidUnicodeEscapeReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\u{GGGG}";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    [TestMethod]
    public void UnclosedUnicodeBraceReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\u{41";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    [TestMethod]
    public void TruncatedHexEscapeReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\x4";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    [TestMethod]
    public void TruncatedControlEscapeReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\c";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    [TestMethod]
    public void UnclosedPropertyEscapeReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\p{L";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    [TestMethod]
    public void UnclosedCharacterClassReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = "[abc";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    [TestMethod]
    public void TruncatedUnicodeEscapeReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\u00";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    [TestMethod]
    public void CodePointAboveMaxReturnsInvalidData()
    {
        ReadOnlySpan<char> ecma = @"\u{110000}";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status);
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

    [TestMethod]
    [DataRow(@"\0", @"\0")]              // NUL at end of pattern
    [DataRow(@"\0a", @"\0a")]            // NUL followed by letter
    [DataRow(@"\0\n", @"\0\n")]          // NUL followed by escape
    [DataRow(@"x\0y", @"x\0y")]         // NUL in middle of pattern
    public void NullEscapeNotFollowedByDigitTranslatesCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"\0", "\0", true)]         // NUL matches NUL
    [DataRow(@"\0", "0", false)]         // NUL does not match '0'
    [DataRow(@"\0", "a", false)]         // NUL does not match 'a'
    [DataRow(@"\0a", "\0a", true)]       // NUL then literal 'a'
    public void NullEscapeMatchesNullCharacter(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // --- \0 followed by digit = INVALID in /u mode (octal forbidden) ---

    [TestMethod]
    [DataRow(@"\00")]     // \0 + '0'
    [DataRow(@"\01")]     // \0 + '1' — would be octal 1 in .NET
    [DataRow(@"\02")]     // \0 + '2'
    [DataRow(@"\03")]     // \0 + '3'
    [DataRow(@"\07")]     // \0 + '7' — highest single-digit octal
    [DataRow(@"\08")]     // \0 + '8' — not even valid octal, still forbidden
    [DataRow(@"\09")]     // \0 + '9'
    public void NullEscapeFollowedByDigitIsInvalid(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    // --- \0 followed by digit inside character class = INVALID ---

    [TestMethod]
    [DataRow(@"[\01]")]     // \0 + '1' inside class
    [DataRow(@"[\07]")]     // \0 + '7' inside class
    [DataRow(@"[\09]")]     // \0 + '9' inside class
    public void NullEscapeFollowedByDigitInCharClassIsInvalid(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    // --- \0 in character class without trailing digit = valid ---

    [TestMethod]
    [DataRow(@"[\0]", "\0", true)]       // NUL in char class
    [DataRow(@"[\0]", "a", false)]
    [DataRow(@"[\0a]", "\0", true)]      // NUL or 'a' class
    [DataRow(@"[\0a]", "a", true)]
    [DataRow(@"[\0a]", "b", false)]
    public void NullEscapeInCharClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // --- Backreferences (NOT octal) in /u mode ---

    [TestMethod]
    [DataRow(@"(a)\1", @"(a)(?(1)\1)")]                  // single digit backref
    [DataRow(@"(a)(b)\2", @"(a)(b)(?(2)\2)")]             // second group
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)\8", @"(a)(b)(c)(d)(e)(f)(g)(h)(?(8)\8)")]   // \8 = backref
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)\9", @"(a)(b)(c)(d)(e)(f)(g)(h)(i)(?(9)\9)")]  // \9 = backref
    public void SingleDigitBackrefsAreNeverOctal(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"(a)\1", "aa", true)]
    [DataRow(@"(a)\1", "ab", false)]
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)\8", "abcdefghh", true)]
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)\8", "abcdefghx", false)]
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)\9", "abcdefghii", true)]
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)\9", "abcdefghix", false)]
    public void SingleDigitBackrefsMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // --- Multi-digit backreferences ---

    [TestMethod]
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10", "abcdefghijj", true)]
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10", "abcdefghijk", false)]
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)\11", "abcdefghijkk", true)]
    [DataRow(@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)\11", "abcdefghijkx", false)]
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

    [TestMethod]
    [DataRow(@"\a")]      // would be BEL in .NET!
    [DataRow(@"\e")]      // would be ESC in .NET!
    [DataRow(@"\q")]
    [DataRow(@"\i")]
    [DataRow(@"\m")]
    [DataRow(@"\o")]
    [DataRow(@"\y")]
    [DataRow(@"\z")]
    [DataRow(@"\g")]
    [DataRow(@"\h")]
    [DataRow(@"\j")]
    [DataRow(@"\l")]
    [DataRow(@"\A")]
    [DataRow(@"\C")]
    [DataRow(@"\E")]
    [DataRow(@"\F")]
    [DataRow(@"\G")]
    [DataRow(@"\H")]
    [DataRow(@"\I")]
    [DataRow(@"\J")]
    [DataRow(@"\K")]
    [DataRow(@"\L")]
    [DataRow(@"\M")]
    [DataRow(@"\N")]
    [DataRow(@"\O")]
    [DataRow(@"\Q")]
    [DataRow(@"\R")]
    [DataRow(@"\T")]
    [DataRow(@"\U")]
    [DataRow(@"\V")]
    [DataRow(@"\X")]
    [DataRow(@"\Y")]
    [DataRow(@"\Z")]
    public void InvalidIdentityEscapeOutsideClassReturnsInvalidData(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);
        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    [TestMethod]
    public void EscapedHyphenOutsideClassReturnsInvalidData()
    {
        // \- is only valid inside character classes in /u mode
        ReadOnlySpan<char> pattern = @"\-";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);
        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    // --- Valid identity escapes (SyntaxCharacter + /) ---
    [TestMethod]
    [DataRow(@"\^", @"\^")]
    [DataRow(@"\$", @"\$")]
    [DataRow(@"\.", @"\.")]
    [DataRow(@"\*", @"\*")]
    [DataRow(@"\+", @"\+")]
    [DataRow(@"\?", @"\?")]
    [DataRow(@"\(", @"\(")]
    [DataRow(@"\)", @"\)")]
    [DataRow(@"\[", @"\[")]
    [DataRow(@"\]", @"\]")]
    [DataRow(@"\{", @"\{")]
    [DataRow(@"\}", @"\}")]
    [DataRow(@"\|", @"\|")]
    [DataRow(@"\\", @"\\")]
    [DataRow(@"\/", @"\/")]
    public void ValidIdentityEscapeTranslatesCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // --- Valid identity escapes match literally ---
    [TestMethod]
    [DataRow(@"\^", "^", true)]
    [DataRow(@"\^", "a", false)]
    [DataRow(@"\$", "$", true)]
    [DataRow(@"\.", ".", true)]
    [DataRow(@"\.", "a", false)]
    [DataRow(@"\*", "*", true)]
    [DataRow(@"\+", "+", true)]
    [DataRow(@"\?", "?", true)]
    [DataRow(@"\(", "(", true)]
    [DataRow(@"\)", ")", true)]
    [DataRow(@"\[", "[", true)]
    [DataRow(@"\]", "]", true)]
    [DataRow(@"\{", "{", true)]
    [DataRow(@"\}", "}", true)]
    [DataRow(@"\|", "|", true)]
    [DataRow(@"\\", "\\", true)]
    [DataRow(@"\/", "/", true)]
    public void ValidIdentityEscapeMatchesLiteral(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 35. INVALID ESCAPES INSIDE CHARACTER CLASS — /u mode
    // ================================================================

    [TestMethod]
    [DataRow(@"[\a]")]      // \a invalid in class
    [DataRow(@"[\e]")]      // \e invalid in class
    [DataRow(@"[\q]")]      // \q invalid in class
    [DataRow(@"[\B]")]      // \B invalid in class (only \b = backspace is valid)
    [DataRow(@"[\A]")]
    [DataRow(@"[\E]")]
    [DataRow(@"[\G]")]
    [DataRow(@"[\i]")]
    [DataRow(@"[\j]")]
    [DataRow(@"[\l]")]
    [DataRow(@"[\m]")]
    [DataRow(@"[\o]")]
    [DataRow(@"[\y]")]
    [DataRow(@"[\z]")]
    public void InvalidIdentityEscapeInCharClassReturnsInvalidData(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);
        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    [TestMethod]
    [DataRow(@"(a)[\1]")]     // backreference in class (digit escape in class)
    [DataRow(@"[\9]")]        // digit escape in class (no group exists, but invalid regardless)
    public void BackreferenceInCharClassReturnsInvalidData(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);
        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    // --- Valid escapes in character class ---
    [TestMethod]
    [DataRow(@"[\-]", @"[\-]")]         // \- valid inside class
    [DataRow(@"[\^]", @"[\^]")]         // \^ valid inside class
    [DataRow(@"[\]]", @"[\]]")]         // \] valid inside class
    [DataRow(@"[\\]", @"[\\]")]         // \\ valid inside class
    [DataRow(@"[\/]", @"[\/]")]         // \/ valid inside class
    [DataRow(@"[\.]", @"[\.]")]         // \. valid inside class
    [DataRow(@"[\t]", @"[\t]")]         // \t control escape in class
    [DataRow(@"[\n]", @"[\n]")]         // \n control escape in class
    [DataRow(@"[\r]", @"[\r]")]         // \r control escape in class
    [DataRow(@"[\f]", @"[\f]")]         // \f control escape in class
    [DataRow(@"[\v]", @"[\v]")]         // \v control escape in class
    public void ValidEscapeInCharClassTranslatesCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ================================================================
    // 36. CONTROL ESCAPE VALIDATION — /u mode
    // ================================================================

    [TestMethod]
    [DataRow(@"\c0")]       // digit not valid
    [DataRow(@"\c!")]       // punctuation not valid
    [DataRow(@"\c ")]       // space not valid
    [DataRow(@"\c1")]
    [DataRow(@"\c9")]
    public void InvalidControlLetterReturnsInvalidData(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);
        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    [TestMethod]
    [DataRow(@"\cA")]      // valid: uppercase
    [DataRow(@"\cZ")]      // valid: uppercase
    [DataRow(@"\ca")]      // valid: lowercase
    [DataRow(@"\cz")]      // valid: lowercase
    [DataRow(@"\cJ")]      // Control-J = \n
    [DataRow(@"\cM")]      // Control-M = \r
    [DataRow(@"\cI")]      // Control-I = \t
    public void ValidControlLetterTranslatesCorrectly(string ecma)
    {
        Assert.AreEqual(ecma, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"\cA", "\x01", true)]    // Control-A
    [DataRow(@"\cZ", "\x1A", true)]    // Control-Z
    [DataRow(@"\ca", "\x01", true)]    // lowercase = same
    [DataRow(@"\cJ", "\n", true)]      // Control-J = LF
    [DataRow(@"\cM", "\r", true)]      // Control-M = CR
    [DataRow(@"\cI", "\t", true)]      // Control-I = TAB
    [DataRow(@"\cA", "A", false)]
    public void ControlEscapeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // Invalid control letter inside character class
    [TestMethod]
    [DataRow(@"[\c0]")]
    [DataRow(@"[\c!]")]
    public void InvalidControlLetterInCharClassReturnsInvalidData(string ecma)
    {
        ReadOnlySpan<char> pattern = ecma;
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(pattern, buffer, out _);
        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    // ================================================================
    // 37. DOT VS LINE TERMINATORS — ES excludes all four
    // ================================================================

    [TestMethod]
    [DataRow(".", "\n", false)]         // LF
    [DataRow(".", "\r", false)]         // CR
    [DataRow(".", "\u2028", false)]     // Line Separator
    [DataRow(".", "\u2029", false)]     // Paragraph Separator
    [DataRow(".", "\t", true)]          // TAB is fine
    [DataRow(".", "\u00A0", true)]      // NBSP is fine
    [DataRow(".", "\uFEFF", true)]      // BOM is fine
    [DataRow(".", "a", true)]
    public void DotExcludesAllFourLineTerminators(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 38. SUPPLEMENTARY CHARACTER MATCHING
    // ================================================================

    [TestMethod]
    [DataRow(@"\u{1F600}", "\U0001F600", true)]     // 😀
    [DataRow(@"\u{1F600}", "a", false)]
    [DataRow(@"\u{10000}", "\U00010000", true)]      // first supplementary
    [DataRow(@"\u{10FFFF}", "\U0010FFFF", true)]     // max codepoint
    [DataRow(@"\u{FFFF}", "\uFFFF", true)]           // BMP boundary
    [DataRow(@"\u{0}", "\0", true)]                   // NUL via braced syntax
    [DataRow(@"\u{41}", "A", true)]                   // simple BMP
    public void SupplementaryCharacterMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // Dot matches supplementary character as single grapheme in .NET
    [TestMethod]
    public void DotMatchesSingleSupplementaryCharacter()
    {
        AssertMatch(".", "\U0001F600", true);
    }

    // Supplementary character range — known limitation:
    // .NET cannot represent surrogate pair ranges in character classes.
    // [\u{1F600}-\u{1F64F}] translates to surrogate-pair-aware alternation
    // e.g. \uD83D[\uDE00-\uDE4F] for same-high-surrogate ranges.

    [TestMethod]
    [DataRow(@"[\u{1F600}-\u{1F64F}]", "\U0001F600", true)]     // 😀 start
    [DataRow(@"[\u{1F600}-\u{1F64F}]", "\U0001F64F", true)]     // 🙏 end
    [DataRow(@"[\u{1F600}-\u{1F64F}]", "\U0001F300", false)]    // 🌀 outside
    [DataRow(@"[\u{1F600}-\u{1F64F}]", "A", false)]
    public void SupplementaryCharacterRangeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 40. CHARACTER CLASS WITH \u{} RANGES (BMP only)
    // ================================================================

    [TestMethod]
    [DataRow(@"\u{41}", @"\u0041")]                 // simple BMP
    [DataRow(@"\u{0}", @"\u0000")]                  // zero
    [DataRow(@"\u{FF}", @"\u00FF")]                 // two hex digits
    [DataRow(@"\u{FFFF}", @"\uFFFF")]               // BMP max
    [DataRow(@"\u{10000}", @"(?:\uD800\uDC00)")]        // first supplementary → surrogate pair in group
    [DataRow(@"\u{10FFFF}", @"(?:\uDBFF\uDFFF)")]       // max → surrogate pair in group
    [DataRow(@"\u{1F600}", @"(?:\uD83D\uDE00)")]        // emoji → surrogate pair in group
    public void UnicodeEscapeBoundaryValuesTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ================================================================
    // 40. CHARACTER CLASS WITH \u{} RANGES
    // ================================================================

    [TestMethod]
    [DataRow(@"[\u{41}-\u{5A}]", "A", true)]     // A-Z
    [DataRow(@"[\u{41}-\u{5A}]", "Z", true)]
    [DataRow(@"[\u{41}-\u{5A}]", "a", false)]
    [DataRow(@"[\u{41}-\u{5A}]", "@", false)]
    public void UnicodeEscapeInCharClassRangeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // Mixed \u{} and literal in class
    [TestMethod]
    [DataRow(@"[\u{41}bc]", "A", true)]
    [DataRow(@"[\u{41}bc]", "b", true)]
    [DataRow(@"[\u{41}bc]", "c", true)]
    [DataRow(@"[\u{41}bc]", "d", false)]
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

    [TestMethod]
    [DataRow(@"\1(a)", "a", true)]         // forward ref = empty match, then captures
    public void ForwardReferenceMatchesEmptyString(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"(?:a|(b))\1", "a", true)]   // group 1 didn't participate → \1 = empty
    [DataRow(@"(?:a|(b))\1", "bb", true)]  // group 1 = 'b', \1 = 'b'
    public void NonParticipatingGroupBackrefMatchesEmpty(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 42. HEX ESCAPE VALUES
    // ================================================================

    [TestMethod]
    [DataRow(@"\x41", "A", true)]
    [DataRow(@"\x61", "a", true)]
    [DataRow(@"\x00", "\0", true)]
    [DataRow(@"\xFF", "\xFF", true)]
    [DataRow(@"\x41", "B", false)]
    public void HexEscapeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 43. BACKSPACE IN CHARACTER CLASS
    // ================================================================

    [TestMethod]
    [DataRow(@"[\b]", "\x08", true)]     // U+0008 backspace
    [DataRow(@"[\b]", "b", false)]
    [DataRow(@"[\b]", "\n", false)]
    public void BackspaceInCharClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 44. COMPLEX NESTED GROUPS WITH BACKREFERENCES
    // ================================================================

    [TestMethod]
    [DataRow(@"((a)(b))\1\2\3", "ababab", true)]
    [DataRow(@"((a)(b))\1\2\3", "ababba", false)]
    public void NestedGroupBackrefsMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // Backreference with quantifier
    [TestMethod]
    [DataRow(@"(a)\1+", "aa", true)]
    [DataRow(@"(a)\1+", "aaa", true)]
    [DataRow(@"(a)\1+", "a", false)]       // \1 must match once
    [DataRow(@"(a)\1{2}", "aaa", true)]    // \1 must match twice
    [DataRow(@"(a)\1{2}", "aa", false)]
    public void BackreferenceWithQuantifierMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 45. LOOKAHEAD/LOOKBEHIND WITH SHORTHANDS
    // ================================================================

    [TestMethod]
    [DataRow(@"(?=\d)5", "5", true)]
    [DataRow(@"(?=\d)a", "a", false)]
    [DataRow(@"(?<=\d)a", "5a", true)]
    [DataRow(@"(?<=\d)a", "ba", false)]
    [DataRow(@"(?!\w)!", "!", true)]
    public void LookAroundWithShorthandsMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 46. PROPERTY ESCAPE INSIDE CHARACTER CLASS
    // ================================================================

    [TestMethod]
    [DataRow(@"[\p{L}\d]", "a", true)]     // \p{L} matches letter
    [DataRow(@"[\p{L}\d]", "5", true)]     // \d matches digit
    [DataRow(@"[\p{L}\d]", "!", false)]
    [DataRow(@"[\p{Lu}0-9]", "A", true)]   // uppercase letter
    [DataRow(@"[\p{Lu}0-9]", "5", true)]   // digit range
    [DataRow(@"[\p{Lu}0-9]", "a", false)]  // lowercase → no match
    public void PropertyEscapeInCharClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 47. EMPTY PATTERN AND QUANTIFIER EDGE CASES
    // ================================================================

    [TestMethod]
    [DataRow("", "", true)]           // empty pattern matches empty
    [DataRow("", "a", true)]          // empty pattern matches anywhere
    [DataRow("a{0}", "", true)]       // zero-match quantifier
    [DataRow("a{0,0}", "", true)]     // explicit zero-zero
    [DataRow("a{0,0}b", "b", true)]  // zero 'a's then 'b'
    public void EmptyAndZeroQuantifierEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 48. ESCAPED SPECIAL CHARS MATCH LITERALLY
    // ================================================================

    [TestMethod]
    [DataRow(@"\t", "\t", true)]
    [DataRow(@"\t", "t", false)]
    [DataRow(@"\n", "\n", true)]
    [DataRow(@"\r", "\r", true)]
    [DataRow(@"\f", "\f", true)]
    [DataRow(@"\v", "\v", true)]
    public void ControlEscapeCharsMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 49. \p{Nd} vs \d — DIFFERENT CHARACTER SETS
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Nd}", "\u0660", true)]    // Arabic-Indic digit ٠
    [DataRow(@"\d", "\u0660", false)]        // ES \d = [0-9] only
    [DataRow(@"\p{Nd}", "5", true)]
    [DataRow(@"\d", "5", true)]
    [DataRow(@"\w", "\u00E9", false)]        // é not in ASCII \w
    [DataRow(@"\w", "\u00F1", false)]        // ñ not in ASCII \w
    [DataRow(@"\d", "\uFF10", false)]        // fullwidth zero not in ES \d
    public void UnicodePropertyVsShorthandDifference(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 50. ORPHAN BACKREFERENCE — no groups defined
    // ================================================================

    [TestMethod]
    [DataRow(@"\1")]       // no group 1
    [DataRow(@"\2")]       // no group 2
    [DataRow(@"\9")]       // no group 9
    public void OrphanBackreferenceIsInvalid(string ecma)
    {
        // In ES /u mode, orphan backreferences (\N with no group N)
        // are SyntaxErrors. We can't fully validate this without
        // counting groups, but .NET will reject these at compile time.
        // This test documents the expected behavior.
        string dotNetPattern = EcmaRegexTranslator.Translate(ecma);
        Assert.ThrowsExactly<System.Text.RegularExpressions.RegexParseException>(
            () => new Regex(dotNetPattern));
    }

    // ================================================================
    // 51. BACKREFERENCES WITH QUANTIFIERS (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"(a)\1+", "aa", true)]       // backref repeated 1+
    [DataRow(@"(a)\1+", "aaa", true)]      // backref repeated 2+
    [DataRow(@"(a)\1+", "a", false)]       // needs at least one repetition
    [DataRow(@"(a)\1?", "a", true)]        // optional backref = empty
    [DataRow(@"(a)\1?", "aa", true)]       // optional backref = matched
    [DataRow(@"(a)\1{2}", "aaa", true)]    // exact repeat
    [DataRow(@"(a)\1{2}", "aa", false)]    // too few
    [DataRow(@"(a)\1*", "a", true)]        // zero repeats
    [DataRow(@"(a)\1*", "aaaa", true)]     // many repeats
    [DataRow(@"(a)\1{1,3}", "aa", true)]   // range min
    [DataRow(@"(a)\1{1,3}", "aaaa", true)] // range max
    public void BackrefWithQuantifierMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 52. MULTIPLE BACKREFS TO SAME GROUP (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"(a)\1\1", "aaa", true)]
    [DataRow(@"(a)\1\1", "aa", false)]
    [DataRow(@"(ab)\1\1", "ababab", true)]
    public void MultipleBackrefsToSameGroupMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 53. BACKREFS INSIDE ALTERNATION (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"(a)(\1|b)", "aa", true)]
    [DataRow(@"(a)(\1|b)", "ab", true)]
    [DataRow(@"(a)(b|\1)", "aa", true)]
    [DataRow(@"(a)(b|\1)", "ab", true)]
    public void BackrefInsideAlternationMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 54. BACKREFS INSIDE LOOKAHEAD/LOOKBEHIND (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"(a)(?=\1)a", "aa", true)]
    [DataRow(@"(a)(?=\1)b", "ab", false)]
    [DataRow(@"(a)(?!\1)b", "ab", true)]
    [DataRow(@"(a)(?!\1)a", "aa", false)]
    public void BackrefInLookaroundMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 55. NESTED GROUPS WITH MULTIPLE BACKREFS (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"((a))\1\2", "aaa", true)]
    [DataRow(@"((a))\1\2", "aa", false)]
    [DataRow(@"((a)(b))\1\2\3", "ababab", true)]
    public void NestedGroupMultiBackrefsMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 56. QUANTIFIED GROUP WITH BACKREF (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"(a+)\1", "aa", true)]
    [DataRow(@"(a+)\1", "aaaa", true)]
    [DataRow(@"(a+)\1", "aaa", true)]
    [DataRow(@"(a{2})\1", "aaaa", true)]
    [DataRow(@"(a{2})\1", "aaa", false)]
    public void QuantifiedGroupBackrefMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 57. NAMED BACKREFS WITH VARIOUS NAMES (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"(?<name123>a)\k<name123>", "aa", true)]
    [DataRow(@"(?<n>a)\k<n>", "aa", true)]
    [DataRow(@"(?<longGroupName>a)\k<longGroupName>", "aa", true)]
    public void NamedBackrefVariationsMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 58. NON-PARTICIPATING / FORWARD BACKREF EDGE CASES (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"(a)?\1", "", true)]         // optional group didn't capture → empty
    [DataRow(@"(a)?\1", "aa", true)]       // optional group captured
    [DataRow(@"\1+(a)", "a", true)]        // forward ref with quantifier
    [DataRow(@"(?:a|(b))\1c", "ac", true)] // non-participating then literal
    [DataRow(@"(?:a|(b))\1c", "bbc", true)]// participating then literal
    public void NonParticipatingAndForwardBackrefEdgeCases(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 59. REAL-WORLD NAMED BACKREF PATTERNS (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow("(?<quote>[\"']).*?\\k<quote>", "\"hello\"", true)]
    [DataRow("(?<quote>[\"']).*?\\k<quote>", "'world'", true)]
    [DataRow("(?<quote>[\"']).*?\\k<quote>", "\"hello'", false)]
    public void QuoteMatchingWithNamedBackrefWorksCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 60. BACKREF TRANSLATION FORMAT (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"(a)\1+", @"(a)(?(1)\1)+")]
    [DataRow(@"(a)\1{2}", @"(a)(?(1)\1){2}")]
    [DataRow(@"(a)\1\1", @"(a)(?(1)\1)(?(1)\1)")]
    [DataRow(@"((a))\1\2", @"((a))(?(1)\1)(?(2)\2)")]
    [DataRow(@"(?<name123>a)\k<name123>", @"(?<name123>a)(?(name123)\k<name123>)")]
    public void BackrefTranslationFormatIsCorrect(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ================================================================
    // 61. MULTIPLE SUPPLEMENTARY RANGES IN ONE CLASS (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\u{1F600}-\u{1F64F}\u{1F900}-\u{1F9FF}]", "\U0001F600", true)]   // 😀 first range
    [DataRow(@"[\u{1F600}-\u{1F64F}\u{1F900}-\u{1F9FF}]", "\U0001F923", true)]   // 🤣 second range
    [DataRow(@"[\u{1F600}-\u{1F64F}\u{1F900}-\u{1F9FF}]", "\U0001F300", false)]  // 🌀 outside both
    public void MultipleSupplementaryRangesInClassMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 62. MIXED BMP AND SUPPLEMENTARY IN CLASS (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[a-z\u{1F600}-\u{1F64F}]", "a", true)]
    [DataRow(@"[a-z\u{1F600}-\u{1F64F}]", "\U0001F600", true)]
    [DataRow(@"[a-z\u{1F600}-\u{1F64F}]", "0", false)]
    public void MixedBmpAndSupplementaryInClassMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 63. SINGLE SUPPLEMENTARY IN CLASS (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\u{1F600}]", "\U0001F600", true)]
    [DataRow(@"[\u{1F600}]", "\U0001F601", false)]
    [DataRow(@"[\u{1F600}]", "a", false)]
    public void SingleSupplementaryInClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 64. TWO INDIVIDUAL SUPPLEMENTARY CHARS IN CLASS (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\u{1F600}\u{1F601}]", "\U0001F600", true)]
    [DataRow(@"[\u{1F600}\u{1F601}]", "\U0001F601", true)]
    [DataRow(@"[\u{1F600}\u{1F601}]", "\U0001F602", false)]
    public void TwoSupplementaryCharsInClassMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 65. BMP CONTENT SURROUNDING SUPPLEMENTARY RANGE (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[A\u{1F600}-\u{1F64F}Z]", "A", true)]
    [DataRow(@"[A\u{1F600}-\u{1F64F}Z]", "\U0001F600", true)]
    [DataRow(@"[A\u{1F600}-\u{1F64F}Z]", "Z", true)]
    [DataRow(@"[A\u{1F600}-\u{1F64F}Z]", "B", false)]
    public void BmpAroundSupplementaryRangeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 66. ESCAPED CHARS WITH SUPPLEMENTARY (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\-\u{1F600}-\u{1F64F}]", "-", true)]
    [DataRow(@"[\-\u{1F600}-\u{1F64F}]", "\U0001F600", true)]
    public void EscapedCharsWithSupplementaryMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 67. WIDE SUPPLEMENTARY RANGE (DIFFERENT HIGH SURROGATES) (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\u{1F600}-\u{1F9FF}]", "\U0001F600", true)]   // start
    [DataRow(@"[\u{1F600}-\u{1F9FF}]", "\U0001F9FF", true)]   // end
    [DataRow(@"[\u{1F600}-\u{1F9FF}]", "\U0001F700", true)]   // middle
    [DataRow(@"[\u{1F600}-\u{1F9FF}]", "\U0001F300", false)]  // before
    [DataRow(@"[\u{1F600}-\u{1F9FF}]", "\U0001FA00", false)]  // after
    public void WideSupplementaryRangeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 68. FULL SUPPLEMENTARY PLANE RANGE (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\u{10000}-\u{10FFFF}]", "\U00010000", true)]   // first supplementary
    [DataRow(@"[\u{10000}-\u{10FFFF}]", "\U0010FFFF", true)]   // last valid code point
    [DataRow(@"[\u{10000}-\u{10FFFF}]", "\U0001F600", true)]   // emoji in range
    [DataRow(@"[\u{10000}-\u{10FFFF}]", "A", false)]           // BMP
    public void FullSupplementaryRangeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 69. NEGATED SUPPLEMENTARY CLASS (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[^\u{1F600}]", "a", true)]
    [DataRow(@"[^\u{1F600}]", "\U0001F600", false)]
    public void NegatedSingleSupplementaryMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // Negated supplementary ranges use negative lookahead:
    // [^\u{1F600}-\u{1F64F}] → (?!\uD83D[\uDE00-\uDE4F])(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^\uD800-\uDFFF])
    [TestMethod]
    [DataRow(@"[^\u{1F600}-\u{1F64F}]", "a", true)]
    [DataRow(@"[^\u{1F600}-\u{1F64F}]", "\U0001F600", false)]
    [DataRow(@"[^\u{1F600}-\u{1F64F}]", "\U0001F300", true)]
    public void NegatedSupplementaryRangeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 70. \p{...} WITH SUPPLEMENTARY IN CLASS (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\p{L}\u{1F600}]", "a", true)]
    [DataRow(@"[\p{L}\u{1F600}]", "\U0001F600", true)]
    [DataRow(@"[\p{L}\u{1F600}]", "5", false)]
    public void PropertyEscapeWithSupplementaryInClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 71. SHORTHAND WITH SUPPLEMENTARY IN CLASS (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\d\u{1F600}-\u{1F64F}]", "5", true)]
    [DataRow(@"[\d\u{1F600}-\u{1F64F}]", "\U0001F600", true)]
    [DataRow(@"[\d\u{1F600}-\u{1F64F}]", "a", false)]
    public void ShorthandWithSupplementaryInClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 72. SUPPLEMENTARY TRANSLATION FORMAT
    // ================================================================

    [TestMethod]
    [DataRow(@"[a-z\u{1F600}-\u{1F64F}]", @"(?:[a-z]|\uD83D[\uDE00-\uDE4F])")]
    [DataRow(@"[A\u{1F600}-\u{1F64F}Z]", @"(?:[AZ]|\uD83D[\uDE00-\uDE4F])")]
    public void MixedSupplementaryTranslationFormatIsCorrect(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ================================================================
    // 73. GETMAXTRANSLATEDLENGTH COVERS NEW PATTERNS
    // ================================================================

    [TestMethod]
    [DataRow(@"(a)\1")]
    [DataRow(@"(a)(b)\1\2")]
    [DataRow(@"(?<n>a)\k<n>")]
    [DataRow(@"[\u{1F600}-\u{1F64F}]")]
    [DataRow(@"[a-z\u{1F600}-\u{1F64F}\u{1F900}-\u{1F9FF}]")]
    [DataRow(@"[\u{10000}-\u{10FFFF}]")]
    public void GetMaxTranslatedLengthCoversNewPatterns(string ecma)
    {
        int maxLen = EcmaRegexTranslator.GetMaxTranslatedLength(ecma);
        string translated = EcmaRegexTranslator.Translate(ecma);
        Assert.IsTrue(maxLen >= translated.Length,
            $"GetMaxTranslatedLength returned {maxLen} but translated pattern is {translated.Length} chars: {translated}");
    }

    // ================================================================
    // 74. TRYTRANSLATE BUFFER TOO SMALL
    // ================================================================

    [TestMethod]
    [DataRow(@"(a)\1", 5)]
    [DataRow(@"[\u{1F600}-\u{1F64F}]", 10)]
    public void TryTranslateReturnsTooSmallForInsufficientBuffer(string ecma, int bufferSize)
    {
        Span<char> buffer = stackalloc char[bufferSize];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);
        Assert.AreEqual(OperationStatus.DestinationTooSmall, status);
    }

    // ================================================================
    // 75. \u{} WITH LEADING ZEROS (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"\u{00041}", @"\u0041")]
    [DataRow(@"\u{0001F600}", @"(?:\uD83D\uDE00)")]
    [DataRow(@"\u{0}", @"\u0000")]
    [DataRow(@"\u{00}", @"\u0000")]
    public void LeadingZerosInBraceEscapeTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"\u{00041}", "A", true)]
    [DataRow(@"\u{00041}", "B", false)]
    [DataRow(@"\u{0001F600}", "\U0001F600", true)]
    [DataRow(@"\u{0001F600}", "A", false)]
    public void LeadingZerosInBraceEscapeMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 76. MIXED ESCAPE TYPES IN CHAR CLASS RANGES (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\u0041-Z]", "A", true)]
    [DataRow(@"[\u0041-Z]", "M", true)]
    [DataRow(@"[\u0041-Z]", "Z", true)]
    [DataRow(@"[\u0041-Z]", "a", false)]
    [DataRow(@"[\x41-Z]", "A", true)]
    [DataRow(@"[\x41-Z]", "a", false)]
    [DataRow(@"[A-\u{5A}]", "A", true)]
    [DataRow(@"[A-\u{5A}]", "Z", true)]
    [DataRow(@"[A-\u{5A}]", "a", false)]
    [DataRow(@"[\x41-\u{5A}]", "M", true)]
    public void MixedEscapeTypesInCharClassRangesMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 77. SUPPLEMENTARY + LITERAL DASH (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\u{1F600}-]", "\U0001F600", true)]
    [DataRow(@"[\u{1F600}-]", "-", true)]
    [DataRow(@"[\u{1F600}-]", "a", false)]
    public void SupplementaryWithLiteralDashMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 78. NEGATED SHORTHAND + SUPPLEMENTARY INTERACTION (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\D\u{1F600}]", "a", true)]        // \D matches non-digit
    [DataRow(@"[\D\u{1F600}]", "\U0001F600", true)]// supplementary char
    [DataRow(@"[\D\u{1F600}]", "5", false)]        // digit excluded by \D
    [DataRow(@"[^\D\u{1F600}]", "5", true)]        // NOT(\D ∪ {emoji}) = digits
    [DataRow(@"[^\D\u{1F600}]", "a", false)]       // letter in \D
    public void NegatedShorthandWithSupplementaryMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 79. DASH AT BOUNDARIES IN CHARACTER CLASSES (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"[-a]", "-", true)]
    [DataRow(@"[-a]", "a", true)]
    [DataRow(@"[-a]", "b", false)]
    [DataRow(@"[a-]", "-", true)]
    [DataRow(@"[a-]", "a", true)]
    [DataRow(@"[a-]", "b", false)]
    public void DashAtBoundariesInCharClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 80. ALTERNATION EDGE CASES (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"|a", "", true)]
    [DataRow(@"|a", "a", true)]
    [DataRow(@"a|", "", true)]
    [DataRow(@"a|", "a", true)]
    [DataRow(@"||", "", true)]
    public void AlternationEdgeCasesMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 81. EMPTY PATTERN (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"", "", true)]
    [DataRow(@"", "abc", true)]
    public void EmptyPatternMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 82. DOT WITH QUANTIFIERS (JS-validated)
    // ================================================================

    [TestMethod]
    [DataRow(@"a.*b", "aXb", true)]
    [DataRow(@"a.+b", "aXb", true)]
    [DataRow(@"a.{3}b", "aXYZb", true)]
    public void DotWithQuantifiersMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 83. BINARY UNICODE PROPERTIES — translated to .NET equivalents
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Emoji}", "\U0001F600", true)]
    [DataRow(@"\p{Emoji}", "a", false)]
    [DataRow(@"\p{ASCII}", "A", true)]
    [DataRow(@"\p{ASCII}", "\U0001F600", false)]
    [DataRow(@"\p{Alphabetic}", "a", true)]
    [DataRow(@"\p{White_Space}", " ", true)]
    public void BinaryPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 84. SCRIPT MAPPING — Script names mapped to .NET block names
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Script=Latin}", "a", true)]
    [DataRow(@"\p{Script=Latin}", "\u03B1", false)]
    [DataRow(@"\p{sc=Latn}", "a", true)]
    public void ScriptLatinMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 84b. SCRIPT=LATIN EXTENDED — full Latin script coverage
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Script=Latin}", "\u00E9", true)]  // é — Latin-1 Supplement
    [DataRow(@"\p{Script=Latin}", "\u0100", true)]  // Ā — Latin Extended-A
    [DataRow(@"\p{Script=Latin}", "\u0180", true)]  // ƀ — Latin Extended-B
    [DataRow(@"\p{Script=Latin}", "\u1E00", true)]  // Ḁ — Latin Extended Additional
    [DataRow(@"\p{Script=Latin}", "!", false)]       // Punctuation — Common script
    [DataRow(@"\p{Script=Latin}", "0", false)]       // Digit — Common script
    [DataRow(@"\p{Script=Latin}", " ", false)]       // Space — Common script
    public void ScriptLatinExtendedMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 85. WRONG-CASE PROPERTY NAMES — invalid in ES /u mode, rejected
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{letter}")]
    [DataRow(@"\p{LETTER}")]
    public void WrongCasePropertyNameIsRejected(string ecma)
    {
        Span<char> buffer = stackalloc char[256];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);
        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    // ================================================================
    // 86. BINARY PROPERTIES — INSIDE CHARACTER CLASSES
    // BMP-only binary properties can be inlined; supplementary ones cannot.
    // ================================================================

    [TestMethod]
    [DataRow(@"[\p{ASCII}]+", "Hello", true)]
    [DataRow(@"[\p{ASCII}]+", "\u00FF", false)]
    [DataRow(@"[\p{ASCII}\p{L}]+", "Hélló", true)]
    [DataRow(@"[\p{White_Space}]+", " \t\n", true)]
    [DataRow(@"[\p{White_Space}]+", "abc", false)]
    [DataRow(@"[\p{Alphabetic}0-9]+", "abc123", true)]
    [DataRow(@"[\p{Alphabetic}0-9]+", "!!!", false)]
    public void BinaryPropertyInsideClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 87. NEGATED BINARY PROPERTIES (standalone)
    // ================================================================

    [TestMethod]
    [DataRow(@"\P{ASCII}", "\u00FF", true)]
    [DataRow(@"\P{ASCII}", "A", false)]
    [DataRow(@"\P{Alphabetic}", "!", true)]
    [DataRow(@"\P{Alphabetic}", "a", false)]
    [DataRow(@"\P{White_Space}", "x", true)]
    [DataRow(@"\P{White_Space}", " ", false)]
    [DataRow(@"\P{Emoji}", "x", true)]
    [DataRow(@"\P{Emoji}", "\U0001F600", false)]
    public void NegatedBinaryPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 88. MORE SCRIPT MAPPINGS
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Script=Greek}", "\u03B1", true)]
    [DataRow(@"\p{Script=Greek}", "a", false)]
    [DataRow(@"\p{sc=Grek}", "\u03B1", true)]
    [DataRow(@"\p{Script=Cyrillic}", "\u0414", true)]
    [DataRow(@"\p{Script=Cyrillic}", "a", false)]
    [DataRow(@"\p{sc=Cyrl}", "\u0414", true)]
    [DataRow(@"\p{Script=Arabic}", "\u0627", true)]
    [DataRow(@"\p{Script=Arabic}", "a", false)]
    [DataRow(@"\p{Script=Hebrew}", "\u05D0", true)]
    [DataRow(@"\p{Script=Hebrew}", "a", false)]
    [DataRow(@"\p{Script=Devanagari}", "\u0905", true)]
    [DataRow(@"\p{Script=Devanagari}", "a", false)]
    [DataRow(@"\p{Script=Thai}", "\u0E01", true)]
    [DataRow(@"\p{Script=Thai}", "a", false)]
    public void ScriptPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 89. EMOJI EDGE CASES
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Emoji}", "\u2764", true)]   // ❤ Heavy black heart
    [DataRow(@"\p{Emoji}", "\u2600", true)]   // ☀ Sun
    [DataRow(@"\p{Emoji}", "\u00A9", true)]   // © Copyright
    [DataRow(@"\p{Emoji}", "\u00AE", true)]   // ® Registered
    [DataRow(@"\p{Emoji}", "\u0023", true)]   // # (Emoji property includes digits, #, *)
    [DataRow(@"\p{Emoji}", "\u002A", true)]   // *
    [DataRow(@"\p{Emoji}", "0", true)]        // digit 0
    [DataRow(@"\p{Emoji}", "\U0001F601", true)]  // 😁
    [DataRow(@"\p{Emoji}", "\U0001F9FF", true)]  // last in U+1F900-1FFFF range
    [DataRow(@"\p{Emoji}", "Z", false)]
    public void EmojiEdgeCasesMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 90. UNKNOWN/INVALID PROPERTY NAMES — rejected in /u mode
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Bogus}")]
    [DataRow(@"\p{NotAProp}")]
    [DataRow(@"\p{Script=Klingon}")]
    [DataRow(@"\p{gc=XX}")]
    [DataRow(@"\p{foo=bar}")]
    public void UnknownPropertyNameIsRejected(string ecma)
    {
        Span<char> buffer = stackalloc char[256];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);
        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    // ================================================================
    // 91. BINARY \p{Any} AND \p{Assigned}
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Any}", "a", true)]
    [DataRow(@"\p{Any}", "\U0001F600", true)]
    [DataRow(@"\p{Assigned}", "a", true)]
    [DataRow(@"\P{Assigned}", "a", false)]
    public void AnyAndAssignedPropertiesMatch(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 92. NEGATED SCRIPT PROPERTIES
    // ================================================================

    [TestMethod]
    [DataRow(@"\P{Script=Latin}", "\u03B1", true)]   // Greek α — not Latin
    [DataRow(@"\P{Script=Latin}", "a", false)]        // Latin a
    [DataRow(@"\P{sc=Greek}", "a", true)]             // a — not Greek
    [DataRow(@"\P{sc=Greek}", "\u03B1", false)]       // Greek α
    [DataRow(@"\P{Script=Cyrillic}", "a", true)]
    [DataRow(@"\P{Script=Cyrillic}", "\u0414", false)]
    public void NegatedScriptPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 93. ALTERNATION-PATTERN PROPERTIES INSIDE CHARACTER CLASSES
    // ================================================================

    [TestMethod]
    [DataRow(@"[\p{Emoji}]", "\U0001F600", true)]
    [DataRow(@"[\p{Emoji}]", "Z", false)]
    [DataRow(@"[\p{Emoji}a-z]", "a", true)]
    [DataRow(@"[\p{Emoji}a-z]", "\U0001F600", true)]
    [DataRow(@"[\p{Emoji}a-z]", "Z", false)]
    [DataRow(@"[\p{Script=Latin}]", "a", true)]
    [DataRow(@"[\p{Script=Latin}]", "\u00E9", true)]
    [DataRow(@"[\p{Script=Latin}]", "\u03B1", false)]
    [DataRow(@"[\p{Script=Latin}0-9]", "5", true)]
    [DataRow(@"[\p{Script=Latin}0-9]", "\u03B1", false)]
    public void AlternationPropertyInCharClassMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 94. NEGATED CLASS WITH ALTERNATION-PATTERN PROPERTY
    // ================================================================

    [TestMethod]
    [DataRow(@"[^\p{Emoji}]", "Z", true)]
    [DataRow(@"[^\p{Emoji}]", "\U0001F600", false)]
    [DataRow(@"[^\p{Script=Latin}]", "\u03B1", true)]
    [DataRow(@"[^\p{Script=Latin}]", "a", false)]
    public void NegatedClassWithAlternationPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 95. MULTIPLE ALTERNATION PROPERTIES IN ONE CLASS
    // ================================================================

    [TestMethod]
    [DataRow(@"[\p{Emoji}\p{Script=Latin}]", "\U0001F600", true)]
    [DataRow(@"[\p{Emoji}\p{Script=Latin}]", "a", true)]
    [DataRow(@"[\p{Emoji}\p{Script=Latin}]", "\u03B1", false)]
    public void MultipleAlternationPropertiesInClassMatch(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 96. DOUBLE NEGATION — [^\P{L}] = [\p{L}]
    // ================================================================

    [TestMethod]
    [DataRow(@"[^\P{L}]", "a", true)]
    [DataRow(@"[^\P{L}]", "0", false)]
    public void DoubleNegationPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 97. MALFORMED PROPERTY NAMES — rejected as InvalidData
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{}")]
    [DataRow(@"\p{=}")]
    [DataRow(@"\p{gc=}")]
    [DataRow(@"\p{=Latin}")]
    public void MalformedPropertyNameIsRejected(string ecma)
    {
        Span<char> buffer = stackalloc char[256];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);
        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    // ================================================================
    // 98. ADDITIONAL BINARY PROPERTIES
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Hex_Digit}", "A", true)]
    [DataRow(@"\p{Hex_Digit}", "f", true)]
    [DataRow(@"\p{Hex_Digit}", "9", true)]
    [DataRow(@"\p{Hex_Digit}", "\uFF21", true)]   // Fullwidth A
    [DataRow(@"\p{Hex_Digit}", "\uFF46", true)]   // Fullwidth f
    [DataRow(@"\p{Hex_Digit}", "g", false)]
    [DataRow(@"\p{Hex_Digit}", "G", false)]
    [DataRow(@"\P{Hex_Digit}", "g", true)]
    [DataRow(@"\P{Hex_Digit}", "A", false)]
    public void HexDigitPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"\p{Lowercase}", "a", true)]
    [DataRow(@"\p{Lowercase}", "z", true)]
    [DataRow(@"\p{Lowercase}", "\u00AA", true)]    // Feminine ordinal (Other_Lowercase)
    [DataRow(@"\p{Lowercase}", "\u00BA", true)]    // Masculine ordinal (Other_Lowercase)
    [DataRow(@"\p{Lowercase}", "\u02B0", true)]    // Modifier letter small h (Other_Lowercase)
    [DataRow(@"\p{Lowercase}", "\u2170", true)]    // Small Roman numeral I (Other_Lowercase)
    [DataRow(@"\p{Lowercase}", "A", false)]
    [DataRow(@"\p{Lowercase}", "Z", false)]
    [DataRow(@"\p{Lowercase}", "0", false)]
    [DataRow(@"\P{Lowercase}", "A", true)]
    [DataRow(@"\P{Lowercase}", "a", false)]
    public void LowercasePropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"\p{Uppercase}", "A", true)]
    [DataRow(@"\p{Uppercase}", "Z", true)]
    [DataRow(@"\p{Uppercase}", "\u2160", true)]    // Roman numeral I (Other_Uppercase)
    [DataRow(@"\p{Uppercase}", "\u24B6", true)]    // Circled Latin capital A (Other_Uppercase)
    [DataRow(@"\p{Uppercase}", "a", false)]
    [DataRow(@"\p{Uppercase}", "z", false)]
    [DataRow(@"\p{Uppercase}", "0", false)]
    [DataRow(@"\P{Uppercase}", "a", true)]
    [DataRow(@"\P{Uppercase}", "A", false)]
    public void UppercasePropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"\p{ID_Start}", "a", true)]
    [DataRow(@"\p{ID_Start}", "Z", true)]
    [DataRow(@"\p{ID_Start}", "\u00C0", true)]     // Latin capital A with grave (L category)
    [DataRow(@"\p{ID_Start}", "\u2118", true)]     // Weierstrass P (Other_ID_Start)
    [DataRow(@"\p{ID_Start}", "\u212E", true)]     // Estimated symbol (Other_ID_Start)
    [DataRow(@"\p{ID_Start}", "\u1885", true)]     // Mongolian letter Ali Gali (Other_ID_Start)
    [DataRow(@"\p{ID_Start}", "0", false)]
    [DataRow(@"\p{ID_Start}", "_", false)]
    [DataRow(@"\p{ID_Start}", " ", false)]
    [DataRow(@"\P{ID_Start}", "0", true)]
    [DataRow(@"\P{ID_Start}", "a", false)]
    public void IdStartPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"\p{Emoji_Presentation}", "\u231A", true)]    // Watch
    [DataRow(@"\p{Emoji_Presentation}", "\u2614", true)]    // Umbrella with rain
    [DataRow(@"\p{Emoji_Presentation}", "\U0001F600", true)] // Grinning face
    [DataRow(@"\p{Emoji_Presentation}", "\U0001F4A9", true)] // Pile of poo
    [DataRow(@"\p{Emoji_Presentation}", "a", false)]
    [DataRow(@"\p{Emoji_Presentation}", "0", false)]
    [DataRow(@"\P{Emoji_Presentation}", "a", true)]
    [DataRow(@"\P{Emoji_Presentation}", "\u231A", false)]
    public void EmojiPresentationPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    [TestMethod]
    [DataRow(@"\p{Extended_Pictographic}", "\u00A9", true)]  // Copyright sign
    [DataRow(@"\p{Extended_Pictographic}", "\u00AE", true)]  // Registered sign
    [DataRow(@"\p{Extended_Pictographic}", "\u2728", true)]  // Sparkles
    [DataRow(@"\p{Extended_Pictographic}", "\U0001F600", true)] // Grinning face
    [DataRow(@"\p{Extended_Pictographic}", "\U0001F9FF", true)] // Nazar amulet
    [DataRow(@"\p{Extended_Pictographic}", "a", false)]
    [DataRow(@"\p{Extended_Pictographic}", "0", false)]
    [DataRow(@"\P{Extended_Pictographic}", "a", true)]
    [DataRow(@"\P{Extended_Pictographic}", "\u00A9", false)]
    public void ExtendedPictographicPropertyMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 99. LOOKAROUND WITH PROPERTY ESCAPES
    // ================================================================

    [TestMethod]
    [DataRow(@"(?=\p{L}).", "abc", true)]
    [DataRow(@"(?=\p{L}).", "123", false)]
    [DataRow(@"(?<=\p{L})\d", "a5", true)]
    [DataRow(@"(?<=\p{L})\d", " 5", false)]
    [DataRow(@"(?!\p{Emoji}).", "a", true)]
    public void LookaroundWithPropertyEscapeMatchesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 100. QUANTIFIERS ON EXPANDED PROPERTIES
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Script=Latin}+", "hello", true)]
    [DataRow(@"\p{Script=Latin}+", "\u03B1", false)]
    [DataRow(@"\p{Emoji}?x", "x", true)]
    [DataRow(@"\p{Emoji}?x", "\U0001F600x", true)]
    [DataRow(@"\p{Emoji}{2}", "\U0001F600\U0001F601", true)]
    [DataRow(@"\p{Emoji}{2}", "\U0001F600", false)]
    public void QuantifiersOnExpandedPropertiesMatchCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 101. GetMaxTranslatedLength COVERS LARGE SCRIPT PATTERNS
    // ================================================================

    [TestMethod]
    public void GetMaxTranslatedLengthCoversLatinScript()
    {
        string ecma = @"\p{Script=Latin}";
        int maxLen = EcmaRegexTranslator.GetMaxTranslatedLength(ecma);
        string result = EcmaRegexTranslator.Translate(ecma);
        Assert.IsTrue(maxLen >= result.Length,
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
        Assert.AreEqual(shouldMatch, isMatch);
    }
}
