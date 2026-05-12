// <copyright file="EcmaRegexTranslatorTranslationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.CodeGenerator.Tests;

using System.Buffers;
using Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Tests that verify the pattern string translation from ECMAScript to .NET syntax.
/// </summary>
[TestClass]
public class EcmaRegexTranslatorTranslationTests
{
    // ============================
    // Literal pass-through
    // ============================

    [TestMethod]
    [DataRow("abc", "abc")]
    [DataRow("", "")]
    [DataRow("hello world", "hello world")]
    [DataRow("123", "123")]
    public void LiteralsPassThrough(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Dot expansion
    // ============================

    [TestMethod]
    [DataRow(".", @"[^\n\r\u2028\u2029]")]
    [DataRow(".+", @"[^\n\r\u2028\u2029]+")]
    [DataRow("a.b", @"a[^\n\r\u2028\u2029]b")]
    [DataRow("...", @"[^\n\r\u2028\u2029][^\n\r\u2028\u2029][^\n\r\u2028\u2029]")]
    public void DotIsExpandedCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Shorthand character classes (standalone)
    // ============================

    [TestMethod]
    [DataRow(@"\d", "[0-9]")]
    [DataRow(@"\D", "[^0-9]")]
    [DataRow(@"\w", "[a-zA-Z0-9_]")]
    [DataRow(@"\W", "[^a-zA-Z0-9_]")]
    [DataRow(@"\s", @"[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    [DataRow(@"\S", @"[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    public void ShorthandClassesExpandCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Word boundaries
    // ============================

    [TestMethod]
    [DataRow(@"\b", @"(?:(?<=[a-zA-Z0-9_])(?![a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?=[a-zA-Z0-9_]))")]
    [DataRow(@"\B", @"(?:(?<=[a-zA-Z0-9_])(?=[a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?![a-zA-Z0-9_]))")]
    public void WordBoundariesExpandCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Unicode escapes
    // ============================

    [TestMethod]
    [DataRow(@"\u0041", @"\u0041")]             // \uXXXX pass-through
    [DataRow(@"\u{41}", @"\u0041")]              // \u{XX} → \u00XX
    [DataRow(@"\u{0041}", @"\u0041")]            // \u{XXXX} → \uXXXX
    [DataRow(@"\u{1F600}", @"(?:\uD83D\uDE00)")]     // Supplementary → surrogate pair in group
    [DataRow(@"\u{10FFFF}", @"(?:\uDBFF\uDFFF)")]    // Max code point in group
    public void UnicodeEscapesTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Unicode property escapes
    // ============================

    [TestMethod]
    [DataRow(@"\p{L}", @"\p{L}")]                              // Short name pass-through
    [DataRow(@"\p{Lu}", @"\p{Lu}")]                             // Short name pass-through
    [DataRow(@"\p{Letter}", @"\p{L}")]                          // Long name → short name
    [DataRow(@"\p{Uppercase_Letter}", @"\p{Lu}")]               // Long name → short name
    [DataRow(@"\p{Decimal_Number}", @"\p{Nd}")]                 // Long name → short name
    [DataRow(@"\P{Letter}", @"\P{L}")]                          // Negated long name
    [DataRow(@"\P{Number}", @"\P{N}")]                          // Negated long name
    [DataRow(@"\p{gc=L}", @"\p{L}")]                            // gc= prefix stripped
    [DataRow(@"\p{General_Category=Letter}", @"\p{L}")]         // General_Category= prefix stripped + mapped
    [DataRow(@"\p{Script=Latin}",
        "(?:[\\u0041-\\u005A\\u0061-\\u007A\\u00AA\\u00BA\\u00C0-\\u00D6\\u00D8-\\u00F6"
        + "\\u00F8-\\u02B8\\u02E0-\\u02E4\\u1D00-\\u1D25\\u1D2C-\\u1D5C\\u1D62-\\u1D65"
        + "\\u1D6B-\\u1D77\\u1D79-\\u1DBE\\u1E00-\\u1EFF\\u2071\\u207F\\u2090-\\u209C"
        + "\\u212A-\\u212B\\u2132\\u214E\\u2160-\\u2188\\u2C60-\\u2C7F\\uA722-\\uA787"
        + "\\uA78B-\\uA7CD\\uA7D0-\\uA7D1\\uA7D3\\uA7D5-\\uA7DC\\uA7F2-\\uA7FF"
        + "\\uAB30-\\uAB5A\\uAB5C-\\uAB64\\uAB66-\\uAB69\\uFB00-\\uFB06\\uFF21-\\uFF3A"
        + "\\uFF41-\\uFF5A]|\\uD801[\\uDF80-\\uDF85]|\\uD801[\\uDF87-\\uDFB0]"
        + "|\\uD801[\\uDFB2-\\uDFBA]|\\uD837[\\uDF00-\\uDF1E]|\\uD837[\\uDF25-\\uDF2A])")]
    [DataRow(@"\p{sc=Greek}", @"\p{IsGreek}")]                  // sc= → Is prefix
    [DataRow(@"\p{Script_Extensions=Cyrillic}", @"\p{IsCyrillic}")]  // scx → Is prefix
    [DataRow(@"\p{ASCII}", @"[\u0000-\u007F]")]                 // Binary property → class
    [DataRow(@"\P{ASCII}", @"[^\u0000-\u007F]")]                // Negated binary property
    [DataRow(@"\p{White_Space}",
        @"[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    public void PropertyEscapesTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Unicode General Category aliases (from PropertyValueAliases.txt)
    // ============================

    [TestMethod]
    [DataRow(@"\p{digit}", @"\p{Nd}")]                          // digit → Nd (Decimal_Number)
    [DataRow(@"\P{digit}", @"\P{Nd}")]                          // Negated digit
    [DataRow(@"\p{cntrl}", @"\p{Cc}")]                          // cntrl → Cc (Control)
    [DataRow(@"\P{cntrl}", @"\P{Cc}")]                          // Negated cntrl
    [DataRow(@"\p{punct}", @"\p{P}")]                           // punct → P (Punctuation)
    [DataRow(@"\P{punct}", @"\P{P}")]                           // Negated punct
    [DataRow(@"\p{Combining_Mark}", @"\p{M}")]                  // Combining_Mark → M (Mark)
    [DataRow(@"\P{Combining_Mark}", @"\P{M}")]                  // Negated Combining_Mark
    [DataRow(@"^\p{digit}+$", @"^\p{Nd}+$")]                   // digit in context
    [DataRow(@"\p{gc=digit}", @"\p{Nd}")]                       // gc= prefix with alias
    [DataRow(@"\p{General_Category=digit}", @"\p{Nd}")]         // General_Category= prefix with alias
    public void GeneralCategoryAliasesTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Simple character classes (no negated shorthands)
    // ============================

    [TestMethod]
    [DataRow("[abc]", "[abc]")]
    [DataRow("[a-z]", "[a-z]")]
    [DataRow("[^abc]", "[^abc]")]
    [DataRow("[^a-z]", "[^a-z]")]
    [DataRow(@"[\d]", "[0-9]")]
    [DataRow(@"[\w]", "[a-zA-Z0-9_]")]
    [DataRow(@"[\s]", @"[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    [DataRow(@"[^\d]", "[^0-9]")]
    [DataRow(@"[^\w]", "[^a-zA-Z0-9_]")]
    [DataRow(@"[\da-f]", "[0-9a-f]")]
    [DataRow(@"[\w.]", "[a-zA-Z0-9_.]")]
    [DataRow(@"[a\-z]", @"[a\-z]")]
    public void SimpleCharacterClassesTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Character classes with negated shorthands (non-negated class)
    // ============================

    [TestMethod]
    [DataRow(@"[\D]", "[^0-9]")]
    [DataRow(@"[\W]", "[^a-zA-Z0-9_]")]
    [DataRow(@"[a\D]", "(?:[a]|[^0-9])")]
    [DataRow(@"[a-z\D]", "(?:[a-z]|[^0-9])")]
    [DataRow(@"[\D\W]", "(?:[^0-9]|[^a-zA-Z0-9_])")]
    public void NonNegatedCharClassWithNegShorthandsUsesAlternation(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Character classes with negated shorthands (negated class)
    // ============================

    [TestMethod]
    [DataRow(@"[^\D]", "[0-9]")]
    [DataRow(@"[^\W]", "[a-zA-Z0-9_]")]
    [DataRow(@"[^a\D]", "[0-9-[a]]")]
    [DataRow(@"[^a-f\D]", "[0-9-[a-f]]")]
    [DataRow(@"[^\D\W]", "[0-9]")]  // intersection of [0-9] and [a-zA-Z0-9_] = [0-9]
    public void NegatedCharClassWithNegShorthandsUsesSubtraction(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Empty and match-all character classes
    // ============================

    [TestMethod]
    [DataRow("[]", @"[^\s\S]")]     // Empty class → matches nothing
    [DataRow("[^]", @"[\s\S]")]      // Match-all class
    public void SpecialCharacterClassesTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Pass-through escapes
    // ============================

    [TestMethod]
    [DataRow(@"\t", @"\t")]
    [DataRow(@"\n", @"\n")]
    [DataRow(@"\r", @"\r")]
    [DataRow(@"\f", @"\f")]
    [DataRow(@"\v", @"\v")]
    [DataRow(@"\0", @"\0")]
    [DataRow(@"\\", @"\\")]
    [DataRow(@"\.", @"\.")]
    [DataRow(@"\*", @"\*")]
    [DataRow(@"\+", @"\+")]
    [DataRow(@"\?", @"\?")]
    [DataRow(@"\^", @"\^")]
    [DataRow(@"\$", @"\$")]
    [DataRow(@"\|", @"\|")]
    [DataRow(@"\(", @"\(")]
    [DataRow(@"\)", @"\)")]
    [DataRow(@"\[", @"\[")]
    [DataRow(@"\]", @"\]")]
    [DataRow(@"\{", @"\{")]
    [DataRow(@"\}", @"\}")]
    [DataRow(@"\/", @"\/")]
    public void EscapeSequencesPassThrough(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Hex and control escapes
    // ============================

    [TestMethod]
    [DataRow(@"\x41", @"\x41")]
    [DataRow(@"\x0A", @"\x0A")]
    [DataRow(@"\cA", @"\cA")]
    [DataRow(@"\cZ", @"\cZ")]
    public void HexAndControlEscapesPassThrough(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Groups
    // ============================

    [TestMethod]
    [DataRow("(abc)", "(abc)")]
    [DataRow("(?:abc)", "(?:abc)")]
    [DataRow("(?=abc)", "(?=abc)")]
    [DataRow("(?!abc)", "(?!abc)")]
    [DataRow("(?<=abc)", "(?<=abc)")]
    [DataRow("(?<!abc)", "(?<!abc)")]
    [DataRow("(?<name>abc)", "(?<name>abc)")]
    public void GroupsPassThrough(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Backreferences
    // ============================

    [TestMethod]
    [DataRow(@"(a)\1", @"(a)(?(1)\1)")]
    [DataRow(@"(a)(b)\2", @"(a)(b)(?(2)\2)")]
    [DataRow(@"(?<name>a)\k<name>", @"(?<name>a)(?(name)\k<name>)")]
    public void BackreferencesPassThrough(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Quantifiers
    // ============================

    [TestMethod]
    [DataRow("a*", "a*")]
    [DataRow("a+", "a+")]
    [DataRow("a?", "a?")]
    [DataRow("a*?", "a*?")]
    [DataRow("a+?", "a+?")]
    [DataRow("a??", "a??")]
    [DataRow("a{3}", "a{3}")]
    [DataRow("a{3,}", "a{3,}")]
    [DataRow("a{3,5}", "a{3,5}")]
    [DataRow("a{3,5}?", "a{3,5}?")]
    public void QuantifiersPassThrough(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Anchors and alternation
    // ============================

    [TestMethod]
    [DataRow("^abc$", "^abc$")]
    [DataRow("a|b|c", "a|b|c")]
    [DataRow("^(a|b)$", "^(a|b)$")]
    public void AnchorsAndAlternationPassThrough(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Complex patterns
    // ============================

    [TestMethod]
    public void DatePatternTranslatesCorrectly()
    {
        // ECMAScript: ^\d{4}-\d{2}-\d{2}$
        string ecma = @"^\d{4}-\d{2}-\d{2}$";
        string result = EcmaRegexTranslator.Translate(ecma);
        Assert.AreEqual("^[0-9]{4}-[0-9]{2}-[0-9]{2}$", result);
    }

    [TestMethod]
    public void WordBoundaryPatternTranslatesCorrectly()
    {
        // \bfoo\b
        string ecma = @"\bfoo\b";
        string result = EcmaRegexTranslator.Translate(ecma);
        string wb = @"(?:(?<=[a-zA-Z0-9_])(?![a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?=[a-zA-Z0-9_]))";
        Assert.AreEqual($"{wb}foo{wb}", result);
    }

    [TestMethod]
    public void EmailLikePatternTranslatesCorrectly()
    {
        // [\w.+-]+@[\w-]+\.[\w.]+
        string ecma = @"[\w.+-]+@[\w-]+\.[\w.]+";
        string result = EcmaRegexTranslator.Translate(ecma);
        Assert.AreEqual(@"[a-zA-Z0-9_.+-]+@[a-zA-Z0-9_-]+\.[a-zA-Z0-9_.]+", result);
    }

    // ============================
    // TryTranslate span API
    // ============================

    [TestMethod]
    public void TryTranslateSucceedsWithAdequateBuffer()
    {
        ReadOnlySpan<char> ecma = @"\d+";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out int written);

        Assert.AreEqual(OperationStatus.Done, status);
        Assert.AreEqual("[0-9]+", new string(buffer[..written]));
    }

    [TestMethod]
    public void TryTranslateReturnsDestinationTooSmallForTinyBuffer()
    {
        ReadOnlySpan<char> ecma = @"\d+";
        Span<char> buffer = stackalloc char[2];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.AreEqual(OperationStatus.DestinationTooSmall, status);
    }

    [TestMethod]
    public void TryTranslateReturnsInvalidDataForBadEscape()
    {
        // Lone backslash at end of pattern
        ReadOnlySpan<char> ecma = @"abc\";
        Span<char> buffer = stackalloc char[64];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(ecma, buffer, out _);

        Assert.AreEqual(OperationStatus.InvalidData, status);
    }

    [TestMethod]
    public void GetMaxTranslatedLengthReturnsAdequateSize()
    {
        string ecma = @"\b\w+\b";
        int maxLen = EcmaRegexTranslator.GetMaxTranslatedLength(ecma);
        string result = EcmaRegexTranslator.Translate(ecma);

        Assert.IsTrue(maxLen >= result.Length, $"MaxLen {maxLen} < actual {result.Length}");
    }

    // ============================
    // Character class with escapes inside
    // ============================

    [TestMethod]
    [DataRow(@"[\b]", @"[\b]")]         // \b = backspace inside char class
    [DataRow(@"[\x41]", @"[\x41]")]     // hex escape inside class
    [DataRow(@"[\u0041]", @"[\u0041]")] // unicode escape inside class
    [DataRow(@"[\u{41}]", @"[\u0041]")] // brace unicode escape inside class
    public void EscapesInsideCharClassTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Unicode property escapes inside character classes
    // ============================

    [TestMethod]
    [DataRow(@"[\p{L}]", @"[\p{L}]")]
    [DataRow(@"[\p{Letter}]", @"[\p{L}]")]
    [DataRow(@"[^\p{L}]", @"[^\p{L}]")]
    public void PropertyEscapesInsideCharClassTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // All General Category long name mappings
    // ============================

    [TestMethod]
    [DataRow("Letter", "L")]
    [DataRow("Cased_Letter", "LC")]
    [DataRow("Uppercase_Letter", "Lu")]
    [DataRow("Lowercase_Letter", "Ll")]
    [DataRow("Titlecase_Letter", "Lt")]
    [DataRow("Modifier_Letter", "Lm")]
    [DataRow("Other_Letter", "Lo")]
    [DataRow("Mark", "M")]
    [DataRow("Nonspacing_Mark", "Mn")]
    [DataRow("Spacing_Mark", "Mc")]
    [DataRow("Enclosing_Mark", "Me")]
    [DataRow("Number", "N")]
    [DataRow("Decimal_Number", "Nd")]
    [DataRow("Letter_Number", "Nl")]
    [DataRow("Other_Number", "No")]
    [DataRow("Punctuation", "P")]
    [DataRow("Connector_Punctuation", "Pc")]
    [DataRow("Dash_Punctuation", "Pd")]
    [DataRow("Open_Punctuation", "Ps")]
    [DataRow("Close_Punctuation", "Pe")]
    [DataRow("Initial_Punctuation", "Pi")]
    [DataRow("Final_Punctuation", "Pf")]
    [DataRow("Other_Punctuation", "Po")]
    [DataRow("Symbol", "S")]
    [DataRow("Math_Symbol", "Sm")]
    [DataRow("Currency_Symbol", "Sc")]
    [DataRow("Modifier_Symbol", "Sk")]
    [DataRow("Other_Symbol", "So")]
    [DataRow("Separator", "Z")]
    [DataRow("Space_Separator", "Zs")]
    [DataRow("Line_Separator", "Zl")]
    [DataRow("Paragraph_Separator", "Zp")]
    [DataRow("Other", "C")]
    [DataRow("Control", "Cc")]
    [DataRow("Format", "Cf")]
    [DataRow("Surrogate", "Cs")]
    [DataRow("Private_Use", "Co")]
    [DataRow("Unassigned", "Cn")]
    public void AllCategoryLongNamesMapCorrectly(string longName, string expectedShort)
    {
        string ecma = $@"\p{{{longName}}}";
        string expected = $@"\p{{{expectedShort}}}";
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Literal non-BMP characters (surrogate pairs in UTF-16)
    // ============================

    [TestMethod]
    [DataRow("^\U0001F432*$", @"^(?:\uD83D\uDC32)*$")]                // 🐲* — quantifier on whole code point
    [DataRow("\U0001F432", @"(?:\uD83D\uDC32)")]                       // bare literal
    [DataRow("\U0001F432+", @"(?:\uD83D\uDC32)+")]                     // quantifier +
    [DataRow("\U0001F432?", @"(?:\uD83D\uDC32)?")]                     // quantifier ?
    [DataRow("\U0001F432{2}", @"(?:\uD83D\uDC32){2}")]                 // quantifier {n}
    [DataRow("\U0001F432\U0001F409", @"(?:\uD83D\uDC32)(?:\uD83D\uDC09)")] // two literals
    [DataRow("a\U0001F432b", @"a(?:\uD83D\uDC32)b")]                  // mixed with ASCII
    public void LiteralNonBmpCharactersTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // \u{XXXXX} escapes wrap supplementary in non-capturing group
    // ============================

    [TestMethod]
    [DataRow(@"\u{1F432}", @"(?:\uD83D\uDC32)")]
    [DataRow(@"\u{1F432}*", @"(?:\uD83D\uDC32)*")]
    [DataRow(@"\u{1F432}+", @"(?:\uD83D\uDC32)+")]
    [DataRow(@"\u{41}", @"\u0041")]                  // BMP code point — no group needed
    public void UnicodeEscapeBraceFormWrapsSupplementaryCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Non-BMP inside character classes
    // ============================

    [TestMethod]
    public void NonBmpLiteralInCharClassProducesAlternation()
    {
        // [\U0001F432] → alternation with surrogate pair
        string result = EcmaRegexTranslator.Translate("[\U0001F432]");
        StringAssert.Contains(result, @"\uD83D");
        StringAssert.Contains(result, @"\uDC32");
    }

    [TestMethod]
    public void NonBmpLiteralWithBmpInCharClassProducesAlternation()
    {
        // [a\U0001F432z] → (?:[az]|\uD83D\uDC32)
        string result = EcmaRegexTranslator.Translate("[a\U0001F432z]");
        StringAssert.Contains(result, @"\uD83D");
        StringAssert.Contains(result, @"\uDC32");
        // Should also contain the BMP chars
        StringAssert.Contains(result, "a");
        StringAssert.Contains(result, "z");
    }
}
