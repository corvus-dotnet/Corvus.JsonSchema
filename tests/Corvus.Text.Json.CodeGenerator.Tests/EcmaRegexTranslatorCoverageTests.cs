// <copyright file="EcmaRegexTranslatorCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.CodeGenerator.Tests;

using System.Buffers;
using System.Text.RegularExpressions;
using Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Coverage tests targeting uncovered Unicode property escape translations,
/// script name mappings, long category names, and error paths in EcmaRegexTranslator.
/// </summary>
[TestClass]
public class EcmaRegexTranslatorCoverageTests
{
    // ================================================================
    // 1. LONG UNICODE CATEGORY NAMES — targets L384-410
    //    Each exercises a specific branch in TryMapCategoryLongName.
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Letter_Number}", "\u16EE", true)]   // ᛮ — runic arlaug (Nl)
    [DataRow(@"\p{Letter_Number}", "5", false)]
    [DataRow(@"\p{Other_Number}", "\u00B2", true)]    // ² — superscript two (No)
    [DataRow(@"\p{Other_Number}", "5", false)]
    [DataRow(@"\p{Punctuation}", ".", true)]
    [DataRow(@"\p{Punctuation}", "a", false)]
    [DataRow(@"\p{Connector_Punctuation}", "_", true)]  // Pc
    [DataRow(@"\p{Connector_Punctuation}", ".", false)]
    [DataRow(@"\p{Dash_Punctuation}", "-", true)]       // Pd
    [DataRow(@"\p{Dash_Punctuation}", ".", false)]
    [DataRow(@"\p{Open_Punctuation}", "(", true)]       // Ps
    [DataRow(@"\p{Open_Punctuation}", "a", false)]
    [DataRow(@"\p{Close_Punctuation}", ")", true)]      // Pe
    [DataRow(@"\p{Close_Punctuation}", "a", false)]
    [DataRow(@"\p{Initial_Punctuation}", "\u00AB", true)]  // « — Pi
    [DataRow(@"\p{Initial_Punctuation}", "a", false)]
    [DataRow(@"\p{Final_Punctuation}", "\u00BB", true)]    // » — Pf
    [DataRow(@"\p{Final_Punctuation}", "a", false)]
    [DataRow(@"\p{Other_Punctuation}", "!", true)]      // Po
    [DataRow(@"\p{Other_Punctuation}", "a", false)]
    [DataRow(@"\p{Symbol}", "$", true)]
    [DataRow(@"\p{Symbol}", "a", false)]
    [DataRow(@"\p{Math_Symbol}", "+", true)]            // Sm
    [DataRow(@"\p{Math_Symbol}", "a", false)]
    [DataRow(@"\p{Currency_Symbol}", "$", true)]        // Sc
    [DataRow(@"\p{Currency_Symbol}", "a", false)]
    [DataRow(@"\p{Modifier_Symbol}", "^", true)]        // Sk
    [DataRow(@"\p{Modifier_Symbol}", "a", false)]
    [DataRow(@"\p{Other_Symbol}", "\u00A9", true)]      // © — So
    [DataRow(@"\p{Other_Symbol}", "a", false)]
    [DataRow(@"\p{Separator}", " ", true)]
    [DataRow(@"\p{Separator}", "a", false)]
    [DataRow(@"\p{Space_Separator}", " ", true)]        // Zs
    [DataRow(@"\p{Space_Separator}", "a", false)]
    [DataRow(@"\p{Line_Separator}", "\u2028", true)]    // Zl
    [DataRow(@"\p{Line_Separator}", "a", false)]
    [DataRow(@"\p{Paragraph_Separator}", "\u2029", true)]  // Zp
    [DataRow(@"\p{Paragraph_Separator}", "a", false)]
    [DataRow(@"\p{Other}", "\u0000", true)]             // C (Cc)
    [DataRow(@"\p{Other}", "a", false)]
    [DataRow(@"\p{Control}", "\u0001", true)]           // Cc
    [DataRow(@"\p{Control}", "a", false)]
    [DataRow(@"\p{cntrl}", "\u001F", true)]             // alias for Cc
    [DataRow(@"\p{cntrl}", "a", false)]
    [DataRow(@"\p{Format}", "\u200B", true)]            // Cf — zero width space
    [DataRow(@"\p{Format}", "a", false)]
    [DataRow(@"\p{Private_Use}", "\uE000", true)]       // Co
    [DataRow(@"\p{Private_Use}", "a", false)]
    [DataRow(@"\p{Unassigned}", "\u0378", true)]        // Cn — unassigned in Greek block
    [DataRow(@"\p{Unassigned}", "a", false)]
    [DataRow(@"\p{punct}", ",", true)]                  // alias for P
    [DataRow(@"\p{punct}", "a", false)]
    [DataRow(@"\p{digit}", "5", true)]                  // alias for Nd
    [DataRow(@"\p{digit}", "a", false)]
    public void LongCategoryNamesTranslateCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 2. SCRIPT NAMES — targets L548-634 (Armenian through Lao)
    //    Each script name maps to a .NET block name.
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Script=Armenian}", "\u0531", true)]   // Ա — Armenian capital Ayb
    [DataRow(@"\p{Script=Armenian}", "a", false)]
    [DataRow(@"\p{sc=Armn}", "\u0531", true)]
    [DataRow(@"\p{Script=Thai}", "\u0E01", true)]       // ก — Thai Ko Kai
    [DataRow(@"\p{Script=Thai}", "a", false)]
    [DataRow(@"\p{sc=Thai}", "\u0E01", true)]
    [DataRow(@"\p{Script=Georgian}", "\u10D0", true)]   // ა — Georgian An
    [DataRow(@"\p{Script=Georgian}", "a", false)]
    [DataRow(@"\p{sc=Geor}", "\u10D0", true)]
    [DataRow(@"\p{Script=Hangul}", "\u1100", true)]     // ᄀ — Hangul Choseong Kiyeok
    [DataRow(@"\p{Script=Hangul}", "a", false)]
    [DataRow(@"\p{sc=Hang}", "\u1100", true)]
    [DataRow(@"\p{Script=Hiragana}", "\u3042", true)]   // あ — Hiragana A
    [DataRow(@"\p{Script=Hiragana}", "a", false)]
    [DataRow(@"\p{sc=Hira}", "\u3042", true)]
    [DataRow(@"\p{Script=Katakana}", "\u30A2", true)]   // ア — Katakana A
    [DataRow(@"\p{Script=Katakana}", "a", false)]
    [DataRow(@"\p{sc=Kana}", "\u30A2", true)]
    [DataRow(@"\p{Script=Tibetan}", "\u0F00", true)]    // ༀ — Tibetan Om
    [DataRow(@"\p{Script=Tibetan}", "a", false)]
    [DataRow(@"\p{sc=Tibt}", "\u0F00", true)]
    [DataRow(@"\p{Script=Bengali}", "\u0985", true)]    // অ — Bengali A
    [DataRow(@"\p{Script=Bengali}", "a", false)]
    [DataRow(@"\p{sc=Beng}", "\u0985", true)]
    [DataRow(@"\p{Script=Devanagari}", "\u0905", true)] // अ — Devanagari A
    [DataRow(@"\p{Script=Devanagari}", "a", false)]
    [DataRow(@"\p{sc=Deva}", "\u0905", true)]
    [DataRow(@"\p{Script=Gujarati}", "\u0A85", true)]   // અ — Gujarati A
    [DataRow(@"\p{Script=Gujarati}", "a", false)]
    [DataRow(@"\p{sc=Gujr}", "\u0A85", true)]
    [DataRow(@"\p{Script=Tamil}", "\u0B85", true)]      // அ — Tamil A
    [DataRow(@"\p{Script=Tamil}", "a", false)]
    [DataRow(@"\p{sc=Taml}", "\u0B85", true)]
    [DataRow(@"\p{Script=Telugu}", "\u0C05", true)]     // అ — Telugu A
    [DataRow(@"\p{Script=Telugu}", "a", false)]
    [DataRow(@"\p{sc=Telu}", "\u0C05", true)]
    [DataRow(@"\p{Script=Kannada}", "\u0C85", true)]    // ಅ — Kannada A
    [DataRow(@"\p{Script=Kannada}", "a", false)]
    [DataRow(@"\p{sc=Knda}", "\u0C85", true)]
    [DataRow(@"\p{Script=Malayalam}", "\u0D05", true)]   // അ — Malayalam A
    [DataRow(@"\p{Script=Malayalam}", "a", false)]
    [DataRow(@"\p{sc=Mlym}", "\u0D05", true)]
    [DataRow(@"\p{Script=Sinhala}", "\u0D85", true)]    // අ — Sinhala A
    [DataRow(@"\p{Script=Sinhala}", "a", false)]
    [DataRow(@"\p{sc=Sinh}", "\u0D85", true)]
    [DataRow(@"\p{Script=Myanmar}", "\u1000", true)]    // က — Myanmar Ka
    [DataRow(@"\p{Script=Myanmar}", "a", false)]
    [DataRow(@"\p{sc=Mymr}", "\u1000", true)]
    [DataRow(@"\p{Script=Ethiopic}", "\u1200", true)]   // ሀ — Ethiopic Ha
    [DataRow(@"\p{Script=Ethiopic}", "a", false)]
    [DataRow(@"\p{sc=Ethi}", "\u1200", true)]
    [DataRow(@"\p{Script=Khmer}", "\u1780", true)]      // ក — Khmer Ka
    [DataRow(@"\p{Script=Khmer}", "a", false)]
    [DataRow(@"\p{sc=Khmr}", "\u1780", true)]
    [DataRow(@"\p{Script=Mongolian}", "\u1820", true)]  // ᠠ — Mongolian A
    [DataRow(@"\p{Script=Mongolian}", "a", false)]
    [DataRow(@"\p{sc=Mong}", "\u1820", true)]
    [DataRow(@"\p{Script=Lao}", "\u0E81", true)]        // ກ — Lao Ko
    [DataRow(@"\p{Script=Lao}", "a", false)]
    [DataRow(@"\p{sc=Laoo}", "\u0E81", true)]
    public void ScriptNamesTranslateCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 3. BINARY PROPERTIES — targets remaining uncovered branches
    //    (Hex_Digit, Lowercase, Uppercase, ID_Start, Any, Assigned,
    //     Emoji_Presentation, Extended_Pictographic)
    // ================================================================

    [TestMethod]
    [DataRow(@"\p{Hex_Digit}", "A", true)]
    [DataRow(@"\p{Hex_Digit}", "f", true)]
    [DataRow(@"\p{Hex_Digit}", "G", false)]
    [DataRow(@"\p{Lowercase}", "a", true)]
    [DataRow(@"\p{Lowercase}", "A", false)]
    [DataRow(@"\p{Uppercase}", "A", true)]
    [DataRow(@"\p{Uppercase}", "a", false)]
    [DataRow(@"\p{ID_Start}", "a", true)]
    [DataRow(@"\p{ID_Start}", "5", false)]
    [DataRow(@"\p{Any}", "x", true)]
    [DataRow(@"\p{Assigned}", "a", true)]
    [DataRow(@"\p{Emoji_Presentation}", "\U0001F600", true)]  // 😀
    [DataRow(@"\p{Extended_Pictographic}", "\U0001F600", true)]
    public void BinaryPropertiesTranslateCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 4. SHORT CATEGORIES IN CHARACTER CLASSES — targets L430-436
    //    IsValidShortCategory is called via IsAlternationProperty when
    //    \p{Xx} appears in a [...] character class.
    // ================================================================

    [TestMethod]
    [DataRow(@"[\p{Mn}]+", "\u0300", true)]   // Nonspacing_Mark → L431
    [DataRow(@"[\p{Mn}]+", "a", false)]
    [DataRow(@"[\p{Mc}]+", "\u0903", true)]   // Spacing_Mark → L431
    [DataRow(@"[\p{Me}]+", "\u20DD", true)]   // Enclosing_Mark → L431
    [DataRow(@"[\p{Pc}]+", "_", true)]        // Connector_Punctuation → L433
    [DataRow(@"[\p{Pd}]+", "-", true)]        // Dash_Punctuation → L433
    [DataRow(@"[\p{Ps}]+", "(", true)]        // Open_Punctuation → L433
    [DataRow(@"[\p{Pe}]+", ")", true)]        // Close_Punctuation → L433
    [DataRow(@"[\p{Pi}]+", "\u00AB", true)]   // Initial_Punctuation → L433
    [DataRow(@"[\p{Pf}]+", "\u00BB", true)]   // Final_Punctuation → L433
    [DataRow(@"[\p{Po}]+", "!", true)]        // Other_Punctuation → L433
    [DataRow(@"[\p{Sm}]+", "+", true)]        // Math_Symbol → L434
    [DataRow(@"[\p{Sc}]+", "$", true)]        // Currency_Symbol → L434
    [DataRow(@"[\p{Sk}]+", "^", true)]        // Modifier_Symbol → L434
    [DataRow(@"[\p{So}]+", "\u00A9", true)]   // Other_Symbol → L434
    [DataRow(@"[\p{Zs}]+", " ", true)]        // Space_Separator → L435
    [DataRow(@"[\p{Zl}]+", "\u2028", true)]   // Line_Separator → L435
    [DataRow(@"[\p{Zp}]+", "\u2029", true)]   // Paragraph_Separator → L435
    [DataRow(@"[\p{Cc}]+", "\u0001", true)]   // Control → L436
    [DataRow(@"[\p{Cf}]+", "\u200B", true)]   // Format → L436
    [DataRow(@"[\p{Co}]+", "\uE000", true)]   // Private_Use → L436
    [DataRow(@"[\p{Cn}]+", "\u0378", true)]   // Unassigned → L436
    public void ShortCategoriesInCharClassTranslateCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 5. TranslateOrFallback — targets L259-265 (catch blocks)
    // ================================================================

    [TestMethod]
    public void TranslateOrFallback_InvalidPattern_ReturnsFallback()
    {
        // An invalid ECMAScript regex pattern (e.g., lone \p with no braces)
        // should return the original pattern as fallback.
        string invalid = @"\p{InvalidCategoryThatDoesNotExist}";
        string result = EcmaRegexTranslator.TranslateOrFallback(invalid);
        Assert.AreEqual(invalid, result);
    }

    // ================================================================
    // 6. TryTranslate with too-small buffer — targets L232-233
    //    (OperationStatus.DestinationTooSmall)
    // ================================================================

    [TestMethod]
    public void TryTranslate_BufferTooSmall_ReturnsDestinationTooSmall()
    {
        // A pattern that expands (e.g., \s → whitespace class)
        // should fail with a 1-char buffer.
        Span<char> tinyBuffer = stackalloc char[1];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(@"\s", tinyBuffer, out _);
        Assert.AreEqual(OperationStatus.DestinationTooSmall, status);
    }

    // ================================================================
    // 7. Supplementary code point in \u{XXXXX} form — uses lowercase hex
    //    Targets L353 (HexDigitValue lowercase 'a'-'f' branch)
    // ================================================================

    [TestMethod]
    [DataRow(@"\u{1f600}", "\U0001F600", true)]   // 😀 with lowercase hex
    [DataRow(@"\u{1f600}", "a", false)]
    public void LowercaseHexInCodePointEscapeWorks(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 8. Character class with \D, \W, \S (negated shorthand inside [...])
    //    Targets L689-698 (HasNegD, HasNegW, HasNegS scanning)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\D]+", "abc", true)]        // \D in class → matches non-digits
    [DataRow(@"[\D]+", "123", false)]
    [DataRow(@"[\W]+", "!@#", true)]        // \W in class → matches non-word
    [DataRow(@"[\W]+", "abc", false)]
    [DataRow(@"[\S]+", "abc", true)]        // \S in class → matches non-space
    [DataRow(@"[\S]+", " \t", false)]
    public void NegatedShorthandsInCharClassTranslate(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 9. Character class with \xHH (hex escape inside class)
    //    Targets L755-758 scanning path
    // ================================================================

    [TestMethod]
    [DataRow(@"[\x41-\x5A]+", "ABC", true)]    // A-Z via hex escapes
    [DataRow(@"[\x41-\x5A]+", "abc", false)]
    public void HexEscapeInCharClassScanning(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 10. Character class with \cX (control escape inside class)
    //     Targets L759-762 scanning path
    // ================================================================

    [TestMethod]
    [DataRow(@"[\cA]+", "\u0001", true)]    // Control-A
    [DataRow(@"[\cA]+", "A", false)]
    public void ControlEscapeInCharClassScanning(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 11. Surrogate pair literal in character class
    //     Targets L772-780 (literal non-BMP char in [...])
    // ================================================================

    [TestMethod]
    [DataRow("[\U0001F600]", "\U0001F600", true)]   // 😀 literal in class
    [DataRow("[\U0001F600]", "a", false)]
    public void SurrogatePairLiteralInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 12. \p{...} inside character class with alternation property
    //     Targets L728-752 (scanning for alternation properties in class)
    //     and L811 (IsAlternationProperty returns false for non-script)
    // ================================================================

    [TestMethod]
    [DataRow(@"[\p{Emoji}a-z]+", "\U0001F600", true)]   // Emoji = alternation property
    [DataRow(@"[\p{Emoji}a-z]+", "abc", true)]
    [DataRow(@"[\p{Emoji}a-z]+", "\U0001F4A9", true)]  // 💩 — also emoji
    public void AlternationPropertyInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // HELPER — translate and match
    // ================================================================

    private static void AssertMatch(string ecmaPattern, string input, bool shouldMatch)
    {
        string dotnet = EcmaRegexTranslator.Translate(ecmaPattern.AsSpan());
        Regex re = new(dotnet, RegexOptions.CultureInvariant);
        bool matched = re.IsMatch(input);
        Assert.AreEqual(shouldMatch, matched);
    }

    // ================================================================
    // 13. Named backreferences (\k<name>) — targets L1027-1084
    //     ECMAScript non-participating group semantics:
    //     \k<name> → (?(name)\k<name>) in .NET
    // ================================================================

    [TestMethod]
    [DataRow(@"(?<word>\w+)\s+\k<word>", "hello hello", true)]     // repeated word
    [DataRow(@"(?<word>\w+)\s+\k<word>", "hello world", false)]    // different words
    [DataRow(@"(?<num>\d+)-\k<num>", "42-42", true)]               // repeated number
    [DataRow(@"(?<num>\d+)-\k<num>", "42-99", false)]
    public void NamedBackreferenceTranslatesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 14. Numeric backreferences (\1, \2) — targets L1087-1133
    //     ECMAScript non-participating group semantics:
    //     \1 → (?(1)\1) in .NET
    // ================================================================

    [TestMethod]
    [DataRow(@"(\w+)\s+\1", "hello hello", true)]     // repeated word via \1
    [DataRow(@"(\w+)\s+\1", "hello world", false)]
    [DataRow(@"(\d+)-(\w+)-\1-\2", "42-abc-42-abc", true)]   // multi-group backrefs
    [DataRow(@"(\d+)-(\w+)-\1-\2", "42-abc-42-xyz", false)]
    public void NumericBackreferenceTranslatesCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 15. Character class escapes inside [...] — targets L1765-1828
    //     \d, \w, \s, \u, \x, \c, \p escapes and surrogate pairs inside classes
    //     EmitOneCharClassAtom is only called when class has an alternation
    //     property (e.g. \p{Script=Latin}) combined with other escapes.
    // ================================================================

    [TestMethod]
    [DataRow(@"[\d\w]+", "a1b2", true)]           // \d and \w in same class
    [DataRow(@"[\d\w]+", "!@#", false)]
    [DataRow(@"[\s\d]+", " 1 2", true)]           // \s and \d in same class
    [DataRow(@"[\s\d]+", "abc", false)]
    [DataRow(@"[\u0041-\u005A]+", "ABC", true)]   // Unicode escape range in class
    [DataRow(@"[\u0041-\u005A]+", "abc", false)]
    public void MultiEscapeInCharClassTranslates(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 15b. EmitOneCharClassAtom — targets L1765-1828 specifically
    //      Requires class with alternation property + other escapes/chars
    //      so EmitCharClassContentSkippingAlternationProperties is used.
    // ================================================================

    [TestMethod]
    [DataRow(@"[\p{Script=Latin}\d]+", "a1", true)]     // \d in alternation-property class
    [DataRow(@"[\p{Script=Latin}\d]+", "\u4e00", false)] // CJK not Latin or digit
    [DataRow(@"[\p{Script=Latin}\w]+", "foo_", true)]   // \w in alternation-property class
    [DataRow(@"[\p{Script=Latin}\w]+", "!!!", false)]
    [DataRow(@"[\p{Script=Latin}\s]+", "a b", true)]    // \s in alternation-property class
    [DataRow(@"[\p{Script=Latin}\x41]+", "A", true)]    // \x41 = 'A' in alt-prop class
    [DataRow(@"[\p{Script=Latin}\x41]+", "\u4e00", false)]
    [DataRow(@"[\p{Script=Latin}\u0030-\u0039]+", "a5", true)]  // \u escape range in alt-prop class
    [DataRow(@"[\p{Script=Latin}\u0030-\u0039]+", "!", false)]
    [DataRow(@"[\p{Script=Latin}\-]+", "a-", true)]     // \- identity escape in alt-prop class
    [DataRow(@"[\p{Script=Latin}\0]+", "a\0", true)]    // \0 null in alt-prop class
    [DataRow(@"[\p{Script=Latin}\t]+", "a\t", true)]    // \t tab in alt-prop class
    [DataRow(@"[\p{Script=Latin}\cA]+", "a\x01", true)] // \cA control escape in alt-prop class
    [DataRow(@"[\p{Script=Latin}\cA]+", "!", false)]
    [DataRow(@"[\p{Script=Latin}\p{Nd}]+", "a5", true)] // \p{Nd} inside alt-prop class
    [DataRow(@"[\p{Script=Latin}\p{Nd}]+", "!", false)]
    public void EmitOneCharClassAtomWithAlternationProperty(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 15c. Literal surrogate pair inside char class with alternation property
    //      Targets L1823-1828 (surrogate pair emission via EmitOneCharClassAtom)
    // ================================================================

    [TestMethod]
    [DataRow("[\\p{Script=Latin}\U0001F600]+", "a\U0001F600", true)]   // emoji in alt-prop class
    [DataRow("[\\p{Script=Latin}\U0001F600]+", "!", false)]
    public void LiteralSurrogatePairInAlternationPropertyClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 16. Supplementary Unicode range (surrogate pair range emission)
    //     Targets L2380-2463+ (multi-surrogate pair range splitting)
    //     A range like [\u{10000}-\u{1FFFF}] spans multiple high surrogates
    // ================================================================

    [TestMethod]
    [DataRow(@"[\u{10000}-\u{1FFFF}]+", "\U00010000", true)]   // Linear B Syllable B008 A
    [DataRow(@"[\u{10000}-\u{1FFFF}]+", "\U0001F000", true)]   // Mahjong tile
    [DataRow(@"[\u{10000}-\u{1FFFF}]+", "a", false)]
    [DataRow(@"[\u{10300}-\u{1FAFF}]+", "\U00010300", true)]   // Old Italic
    [DataRow(@"[\u{10300}-\u{1FAFF}]+", "\U0001F600", true)]   // 😀
    [DataRow(@"[\u{10300}-\u{1FAFF}]+", "z", false)]
    public void SupplementaryUnicodeRangeInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 17. Non-BMP literal character outside character class — targets L878-889
    //     A literal emoji outside [...] should be wrapped in (?:...)
    // ================================================================

    [TestMethod]
    [DataRow("\U0001F600+", "\U0001F600\U0001F600", true)]   // 😀+ matches two
    [DataRow("\U0001F600+", "a", false)]
    [DataRow("a\U0001F4A9b", "a\U0001F4A9b", true)]          // Literal 💩 inline
    [DataRow("a\U0001F4A9b", "axb", false)]
    public void NonBmpLiteralOutsideCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }
}
