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
    /// <summary>
    /// How a set that holds every surrogate and every character beyond the Basic Multilingual Plane (the dot, a
    /// negated class) ends: after the class of its other code units come a surrogate pair, a first half with no
    /// second half after it, and a second half with no first half before it.
    /// </summary>
    private const string BeyondBmp = @"|[\uD800-\uDBFF](?:[\uDC00-\uDFFF]|(?![\uDC00-\uDFFF]))|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF])";

    /// <summary>
    /// What a translation starts with when the pattern could otherwise match from between the two halves of a
    /// surrogate pair, where ECMA-262 has no position.
    /// </summary>
    private const string NotInsidePair = @"(?!(?<=[\uD800-\uDBFF])[\uDC00-\uDFFF])";

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
    [DataRow(".", @"(?>[^\n\r\u2028\u2029\uD800-\uDFFF]" + BeyondBmp)]
    [DataRow(".+", @"[^\n\r\u2028\u2029]+")]
    [DataRow("a.b", @"a(?>[^\n\r\u2028\u2029\uD800-\uDFFF]" + BeyondBmp + "b")]
    [DataRow("...", @"(?>[^\n\r\u2028\u2029\uD800-\uDFFF]" + BeyondBmp + @"(?>[^\n\r\u2028\u2029\uD800-\uDFFF]" + BeyondBmp + @"(?>[^\n\r\u2028\u2029\uD800-\uDFFF]" + BeyondBmp)]
    public void DotIsExpandedCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Shorthand character classes (standalone)
    // ============================

    [TestMethod]
    [DataRow(@"\d", "[0-9]")]
    [DataRow(@"\D", @"(?>[^0-9\uD800-\uDFFF]" + BeyondBmp)]
    [DataRow(@"\w", "[a-zA-Z0-9_]")]
    [DataRow(@"\W", @"(?>[^a-zA-Z0-9_\uD800-\uDFFF]" + BeyondBmp)]
    [DataRow(@"\s", @"[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    [DataRow(@"\S", @"(?>[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uD800-\uDFFF]" + BeyondBmp)]
    public void ShorthandClassesExpandCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Word boundaries
    // ============================

    [TestMethod]
    [DataRow(@"\b", @"(?:(?<=[a-zA-Z0-9_])(?![a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?=[a-zA-Z0-9_]))")]
    [DataRow(@"\B", NotInsidePair + "(?:(?<=[a-zA-Z0-9_])(?=[a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?![a-zA-Z0-9_]))")]
    public void WordBoundariesExpandCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Unicode escapes
    // ============================

    [TestMethod]
    [DataRow(@"\u0041", "A")]             // written as the character
    [DataRow(@"\u{41}", "A")]              // written as the character
    [DataRow(@"\u{0041}", "A")]            // written as the character
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
    [DataRow(@"\p{Zs}", @"\p{Zs}")]                             // General category: .NET's own class
    [DataRow(@"\p{Space_Separator}", @"\p{Zs}")]                // Long name → short name
    [DataRow(@"\p{gc=Zs}", @"\p{Zs}")]                          // gc= prefix stripped
    [DataRow(@"\p{General_Category=Space_Separator}", @"\p{Zs}")] // General_Category= prefix stripped + mapped
    [DataRow(@"\P{Zs}", @"(?>[^\p{Zs}\uD800-\uDFFF]" + BeyondBmp)] // Negated: every other character, by code point
    [DataRow(@"\p{Cc}", @"\p{Cc}")]
    [DataRow(@"\p{Pc}", @"\p{Pc}")]
    [DataRow(@"\p{Zl}", @"\p{Zl}")]
    [DataRow(@"\p{ASCII}", @"[\u0000-\u007F]")]                 // Binary property → its set
    [DataRow(@"\P{ASCII}", @"(?>[\u0080-\uD7FF\uE000-\uFFFF]" + BeyondBmp)] // Negated binary property
    [DataRow(@"\p{White_Space}", @"[\t-\r \u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")] // The Unicode property, which is not \s
    [DataRow(@"\p{Any}", @"(?>[^\uD800-\uDFFF]" + BeyondBmp)]
    [DataRow(@"\p{Cs}", @"(?>[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF])")] // A surrogate that is not half of a pair
    public void PropertyEscapesTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    /// <summary>
    /// A property with members beyond the Basic Multilingual Plane is written as its code units that are characters
    /// by themselves (.NET's own class for a general category, and the ranges of the Unicode data for a script or a
    /// binary property), then one alternative of surrogate pairs for each first half, behind a test for a surrogate
    /// when there are more than two. The translations run to thousands of characters, so the test holds how each
    /// starts and that it ends inside the pairs.
    /// </summary>
    [TestMethod]
    [DataRow(@"\p{L}", @"(?>\p{L}|(?=[\uD800-\uDFFF])(?:\uD800[\uDC00-\uDC0B\uDC0D-\uDC26")]
    [DataRow(@"\p{Lu}", @"(?>\p{Lu}|(?=[\uD800-\uDFFF])(?:\uD801[\uDC00-\uDC27\uDCB0-\uDCD3")]
    [DataRow(@"\p{Decimal_Number}", @"(?>\p{Nd}|(?=[\uD800-\uDFFF])(?:\uD801[\uDCA0-\uDCA9]|\uD803[\uDD30-\uDD39")]
    [DataRow(@"\p{LC}", @"(?>[\p{Lu}\p{Ll}\p{Lt}]|(?=[\uD800-\uDFFF])(?:\uD801[\uDC00-\uDC4F")]
    [DataRow(@"\P{Letter}", @"(?>[^\p{L}\uD800-\uDFFF]|(?=[\uD800-\uDFFF])(?:\uD800[\uDC0C\uDC27")]
    [DataRow(@"\P{Number}", @"(?>[^\p{N}\uD800-\uDFFF]|(?=[\uD800-\uDFFF])(?:\uD800[\uDC00-\uDD06")]
    [DataRow(@"\p{Script=Latin}", @"(?>[A-Za-z\u00AA\u00BA\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02B8\u02E0-\u02E4")]
    [DataRow(@"\p{sc=Greek}", @"(?>[\u0370-\u0373\u0375-\u0377\u037A-\u037D\u037F\u0384\u0386")]
    [DataRow(@"\p{Script_Extensions=Cyrillic}", @"(?>[\u02BC\u0300-\u0302\u0304\u0306\u0308\u030B\u0311\u0400-\u052F")]
    public void PropertyEscapesWithSupplementaryMembersStartWithTheirBmpClass(string ecma, string expectedStart)
    {
        string actual = EcmaRegexTranslator.Translate(ecma);
        Assert.AreEqual(expectedStart, actual.Substring(0, Math.Min(actual.Length, expectedStart.Length)));
        Assert.IsTrue(actual.EndsWith("])", StringComparison.Ordinal) || actual.EndsWith("]))", StringComparison.Ordinal), actual.Substring(actual.Length - 20));
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
    [DataRow(@"^\p{digit}+$", @"^\p{Nd}+$")]                    // digit in context
    [DataRow(@"\p{gc=digit}", @"\p{Nd}")]                       // gc= prefix with alias
    [DataRow(@"\p{General_Category=digit}", @"\p{Nd}")]         // General_Category= prefix with alias
    public void GeneralCategoryAliasesTranslateCorrectly(string ecma, string sameAs)
    {
        // An alias is translated as the name it stands for.
        Assert.AreEqual(EcmaRegexTranslator.Translate(sameAs), EcmaRegexTranslator.Translate(ecma));
    }

    [TestMethod]
    [DataRow(@"\p{cntrl}", @"\p{Cc}")]
    [DataRow(@"\P{cntrl}", @"(?>[^\p{Cc}\uD800-\uDFFF]" + BeyondBmp)]
    public void GeneralCategoryAliasTranslationIsExact(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Simple character classes (no negated shorthands)
    // ============================

    [TestMethod]
    [DataRow("[abc]", "[abc]")]
    [DataRow("[a-z]", "[a-z]")]
    [DataRow("[^abc]", @"(?>[^abc\uD800-\uDFFF]" + BeyondBmp)]
    [DataRow("[^a-z]", @"(?>[^a-z\uD800-\uDFFF]" + BeyondBmp)]
    [DataRow(@"[\d]", "[0-9]")]
    [DataRow(@"[\w]", "[a-zA-Z0-9_]")]
    [DataRow(@"[\s]", @"[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]")]
    [DataRow(@"[^\d]", @"(?>[^0-9\uD800-\uDFFF]" + BeyondBmp)]
    [DataRow(@"[^\w]", @"(?>[^a-zA-Z0-9_\uD800-\uDFFF]" + BeyondBmp)]
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
    [DataRow(@"[\D]", @"(?>[^0-9\uD800-\uDFFF]" + BeyondBmp)]
    [DataRow(@"[\W]", @"(?>[^a-zA-Z0-9_\uD800-\uDFFF]" + BeyondBmp)]
    [DataRow(@"[a\D]", @"(?>[^0-9\uD800-\uDFFF]" + BeyondBmp)]
    [DataRow(@"[a-z\D]", @"(?>[^0-9\uD800-\uDFFF]" + BeyondBmp)]
    [DataRow(@"[\D\W]", @"(?>[^0-9\uD800-\uDFFF]" + BeyondBmp)]
    public void NonNegatedCharClassWithNegShorthandsIsWrittenAsItsSet(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Character classes with negated shorthands (negated class)
    // ============================

    [TestMethod]
    [DataRow(@"[^\D]", "[0-9]")]
    [DataRow(@"[^\W]", "[a-zA-Z0-9_]")]
    [DataRow(@"[^a\D]", "[0-9]")]
    [DataRow(@"[^a-f\D]", "[0-9]")]
    [DataRow(@"[^\D\W]", "[0-9]")]  // intersection of [0-9] and [a-zA-Z0-9_] = [0-9]
    public void NegatedCharClassWithNegShorthandsIsWrittenAsItsSet(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Empty and match-all character classes
    // ============================

    [TestMethod]
    [DataRow("[]", @"(?!\z|[\s\S])")]     // Empty class → matches nothing
    [DataRow("[^]", @"(?>[^\uD800-\uDFFF]" + BeyondBmp)]      // Match-all class
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
    [DataRow(@"\0", @"\u0000")]
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
    [DataRow(@"\/", "/")]
    public void EscapeSequencesTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Hex and control escapes
    // ============================

    [TestMethod]
    [DataRow(@"\x41", "A")]
    [DataRow(@"\x0A", @"\n")]
    [DataRow(@"\cA", @"\u0001")]
    [DataRow(@"\cZ", @"\u001A")]
    public void HexAndControlEscapesAreWrittenAsTheirCharacter(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Groups
    // ============================

    [TestMethod]
    [DataRow("(abc)", "(?:abc)")]
    [DataRow("(?:abc)", "(?:abc)")]
    [DataRow("(?=abc)", "(?=abc)")]
    [DataRow("(?!abc)", NotInsidePair + "(?!abc)")]
    [DataRow("(?<=abc)", "(?<=abc)")]
    [DataRow("(?<!abc)", NotInsidePair + "(?<!abc)")]
    [DataRow("(?<name>abc)", "(?:abc)")]
    public void GroupsAreWrittenWithoutCaptureUnlessABackreferenceReadsThem(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Backreferences
    // ============================

    [TestMethod]
    [DataRow(@"(a)\1", @"(?<1>a)(?(1)\1)")]
    [DataRow(@"(a)(b)\2", @"(?:a)(?<2>b)(?(2)\2)")]
    [DataRow(@"(?<name>a)\k<name>", @"(?<1>a)(?(1)\1)")]
    public void BackreferencesAreWrittenAsConditionals(string ecma, string expected)
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
    [DataRow("^abc$", @"^abc\z")]
    [DataRow("a|b|c", "a|b|c")]
    [DataRow("^(a|b)$", @"^(?:a|b)\z")]
    public void AnchorsAndAlternationTranslateCorrectly(string ecma, string expected)
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
        Assert.AreEqual(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}\z", result);
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
        Assert.AreEqual(@"[a-zA-Z0-9_.+\-]+@[a-zA-Z0-9_\-]+\.[a-zA-Z0-9_.]+", result);
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
    [DataRow(@"[\b]", @"[\u0008]")]         // \b = backspace inside char class
    [DataRow(@"[\x41]", "[A]")]     // hex escape inside class
    [DataRow(@"[\u0041]", "[A]")] // unicode escape inside class
    [DataRow(@"[\u{41}]", "[A]")] // brace unicode escape inside class
    public void EscapesInsideCharClassTranslateCorrectly(string ecma, string expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Unicode property escapes inside character classes
    // ============================

    [TestMethod]
    [DataRow(@"[\p{L}]", @"\p{L}")]
    [DataRow(@"[\p{Letter}]", @"\p{L}")]
    [DataRow(@"[^\p{L}]", @"\P{L}")]
    public void PropertyEscapesInsideCharClassTranslateCorrectly(string ecma, string sameAs)
    {
        // A class that holds one property is that property.
        Assert.AreEqual(EcmaRegexTranslator.Translate(sameAs), EcmaRegexTranslator.Translate(ecma));
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
        // A long name is translated as its short name, whose translation the tests above hold.
        string ecma = $@"\p{{{longName}}}";
        string sameAs = $@"\p{{{expectedShort}}}";
        Assert.AreEqual(EcmaRegexTranslator.Translate(sameAs), EcmaRegexTranslator.Translate(ecma));
    }

    // ============================
    // Literal non-BMP characters (surrogate pairs in UTF-16)
    // ============================

    [TestMethod]
    [DataRow("^\U0001F432*$", @"^(?:\uD83D\uDC32)*\z")]                // 🐲* — quantifier on whole code point
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
    [DataRow(@"\u{41}", "A")]                  // BMP code point — no group needed
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