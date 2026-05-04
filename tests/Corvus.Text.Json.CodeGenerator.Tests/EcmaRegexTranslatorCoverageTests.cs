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
public class EcmaRegexTranslatorCoverageTests
{
    // ================================================================
    // 1. LONG UNICODE CATEGORY NAMES — targets L384-410
    //    Each exercises a specific branch in TryMapCategoryLongName.
    // ================================================================

    [Theory]
    [InlineData(@"\p{Letter_Number}", "\u16EE", true)]   // ᛮ — runic arlaug (Nl)
    [InlineData(@"\p{Letter_Number}", "5", false)]
    [InlineData(@"\p{Other_Number}", "\u00B2", true)]    // ² — superscript two (No)
    [InlineData(@"\p{Other_Number}", "5", false)]
    [InlineData(@"\p{Punctuation}", ".", true)]
    [InlineData(@"\p{Punctuation}", "a", false)]
    [InlineData(@"\p{Connector_Punctuation}", "_", true)]  // Pc
    [InlineData(@"\p{Connector_Punctuation}", ".", false)]
    [InlineData(@"\p{Dash_Punctuation}", "-", true)]       // Pd
    [InlineData(@"\p{Dash_Punctuation}", ".", false)]
    [InlineData(@"\p{Open_Punctuation}", "(", true)]       // Ps
    [InlineData(@"\p{Open_Punctuation}", "a", false)]
    [InlineData(@"\p{Close_Punctuation}", ")", true)]      // Pe
    [InlineData(@"\p{Close_Punctuation}", "a", false)]
    [InlineData(@"\p{Initial_Punctuation}", "\u00AB", true)]  // « — Pi
    [InlineData(@"\p{Initial_Punctuation}", "a", false)]
    [InlineData(@"\p{Final_Punctuation}", "\u00BB", true)]    // » — Pf
    [InlineData(@"\p{Final_Punctuation}", "a", false)]
    [InlineData(@"\p{Other_Punctuation}", "!", true)]      // Po
    [InlineData(@"\p{Other_Punctuation}", "a", false)]
    [InlineData(@"\p{Symbol}", "$", true)]
    [InlineData(@"\p{Symbol}", "a", false)]
    [InlineData(@"\p{Math_Symbol}", "+", true)]            // Sm
    [InlineData(@"\p{Math_Symbol}", "a", false)]
    [InlineData(@"\p{Currency_Symbol}", "$", true)]        // Sc
    [InlineData(@"\p{Currency_Symbol}", "a", false)]
    [InlineData(@"\p{Modifier_Symbol}", "^", true)]        // Sk
    [InlineData(@"\p{Modifier_Symbol}", "a", false)]
    [InlineData(@"\p{Other_Symbol}", "\u00A9", true)]      // © — So
    [InlineData(@"\p{Other_Symbol}", "a", false)]
    [InlineData(@"\p{Separator}", " ", true)]
    [InlineData(@"\p{Separator}", "a", false)]
    [InlineData(@"\p{Space_Separator}", " ", true)]        // Zs
    [InlineData(@"\p{Space_Separator}", "a", false)]
    [InlineData(@"\p{Line_Separator}", "\u2028", true)]    // Zl
    [InlineData(@"\p{Line_Separator}", "a", false)]
    [InlineData(@"\p{Paragraph_Separator}", "\u2029", true)]  // Zp
    [InlineData(@"\p{Paragraph_Separator}", "a", false)]
    [InlineData(@"\p{Other}", "\u0000", true)]             // C (Cc)
    [InlineData(@"\p{Other}", "a", false)]
    [InlineData(@"\p{Control}", "\u0001", true)]           // Cc
    [InlineData(@"\p{Control}", "a", false)]
    [InlineData(@"\p{cntrl}", "\u001F", true)]             // alias for Cc
    [InlineData(@"\p{cntrl}", "a", false)]
    [InlineData(@"\p{Format}", "\u200B", true)]            // Cf — zero width space
    [InlineData(@"\p{Format}", "a", false)]
    [InlineData(@"\p{Private_Use}", "\uE000", true)]       // Co
    [InlineData(@"\p{Private_Use}", "a", false)]
    [InlineData(@"\p{Unassigned}", "\u0378", true)]        // Cn — unassigned in Greek block
    [InlineData(@"\p{Unassigned}", "a", false)]
    [InlineData(@"\p{punct}", ",", true)]                  // alias for P
    [InlineData(@"\p{punct}", "a", false)]
    [InlineData(@"\p{digit}", "5", true)]                  // alias for Nd
    [InlineData(@"\p{digit}", "a", false)]
    public void LongCategoryNamesTranslateCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 2. SCRIPT NAMES — targets L548-634 (Armenian through Lao)
    //    Each script name maps to a .NET block name.
    // ================================================================

    [Theory]
    [InlineData(@"\p{Script=Armenian}", "\u0531", true)]   // Ա — Armenian capital Ayb
    [InlineData(@"\p{Script=Armenian}", "a", false)]
    [InlineData(@"\p{sc=Armn}", "\u0531", true)]
    [InlineData(@"\p{Script=Thai}", "\u0E01", true)]       // ก — Thai Ko Kai
    [InlineData(@"\p{Script=Thai}", "a", false)]
    [InlineData(@"\p{sc=Thai}", "\u0E01", true)]
    [InlineData(@"\p{Script=Georgian}", "\u10D0", true)]   // ა — Georgian An
    [InlineData(@"\p{Script=Georgian}", "a", false)]
    [InlineData(@"\p{sc=Geor}", "\u10D0", true)]
    [InlineData(@"\p{Script=Hangul}", "\u1100", true)]     // ᄀ — Hangul Choseong Kiyeok
    [InlineData(@"\p{Script=Hangul}", "a", false)]
    [InlineData(@"\p{sc=Hang}", "\u1100", true)]
    [InlineData(@"\p{Script=Hiragana}", "\u3042", true)]   // あ — Hiragana A
    [InlineData(@"\p{Script=Hiragana}", "a", false)]
    [InlineData(@"\p{sc=Hira}", "\u3042", true)]
    [InlineData(@"\p{Script=Katakana}", "\u30A2", true)]   // ア — Katakana A
    [InlineData(@"\p{Script=Katakana}", "a", false)]
    [InlineData(@"\p{sc=Kana}", "\u30A2", true)]
    [InlineData(@"\p{Script=Tibetan}", "\u0F00", true)]    // ༀ — Tibetan Om
    [InlineData(@"\p{Script=Tibetan}", "a", false)]
    [InlineData(@"\p{sc=Tibt}", "\u0F00", true)]
    [InlineData(@"\p{Script=Bengali}", "\u0985", true)]    // অ — Bengali A
    [InlineData(@"\p{Script=Bengali}", "a", false)]
    [InlineData(@"\p{sc=Beng}", "\u0985", true)]
    [InlineData(@"\p{Script=Devanagari}", "\u0905", true)] // अ — Devanagari A
    [InlineData(@"\p{Script=Devanagari}", "a", false)]
    [InlineData(@"\p{sc=Deva}", "\u0905", true)]
    [InlineData(@"\p{Script=Gujarati}", "\u0A85", true)]   // અ — Gujarati A
    [InlineData(@"\p{Script=Gujarati}", "a", false)]
    [InlineData(@"\p{sc=Gujr}", "\u0A85", true)]
    [InlineData(@"\p{Script=Tamil}", "\u0B85", true)]      // அ — Tamil A
    [InlineData(@"\p{Script=Tamil}", "a", false)]
    [InlineData(@"\p{sc=Taml}", "\u0B85", true)]
    [InlineData(@"\p{Script=Telugu}", "\u0C05", true)]     // అ — Telugu A
    [InlineData(@"\p{Script=Telugu}", "a", false)]
    [InlineData(@"\p{sc=Telu}", "\u0C05", true)]
    [InlineData(@"\p{Script=Kannada}", "\u0C85", true)]    // ಅ — Kannada A
    [InlineData(@"\p{Script=Kannada}", "a", false)]
    [InlineData(@"\p{sc=Knda}", "\u0C85", true)]
    [InlineData(@"\p{Script=Malayalam}", "\u0D05", true)]   // അ — Malayalam A
    [InlineData(@"\p{Script=Malayalam}", "a", false)]
    [InlineData(@"\p{sc=Mlym}", "\u0D05", true)]
    [InlineData(@"\p{Script=Sinhala}", "\u0D85", true)]    // අ — Sinhala A
    [InlineData(@"\p{Script=Sinhala}", "a", false)]
    [InlineData(@"\p{sc=Sinh}", "\u0D85", true)]
    [InlineData(@"\p{Script=Myanmar}", "\u1000", true)]    // က — Myanmar Ka
    [InlineData(@"\p{Script=Myanmar}", "a", false)]
    [InlineData(@"\p{sc=Mymr}", "\u1000", true)]
    [InlineData(@"\p{Script=Ethiopic}", "\u1200", true)]   // ሀ — Ethiopic Ha
    [InlineData(@"\p{Script=Ethiopic}", "a", false)]
    [InlineData(@"\p{sc=Ethi}", "\u1200", true)]
    [InlineData(@"\p{Script=Khmer}", "\u1780", true)]      // ក — Khmer Ka
    [InlineData(@"\p{Script=Khmer}", "a", false)]
    [InlineData(@"\p{sc=Khmr}", "\u1780", true)]
    [InlineData(@"\p{Script=Mongolian}", "\u1820", true)]  // ᠠ — Mongolian A
    [InlineData(@"\p{Script=Mongolian}", "a", false)]
    [InlineData(@"\p{sc=Mong}", "\u1820", true)]
    [InlineData(@"\p{Script=Lao}", "\u0E81", true)]        // ກ — Lao Ko
    [InlineData(@"\p{Script=Lao}", "a", false)]
    [InlineData(@"\p{sc=Laoo}", "\u0E81", true)]
    public void ScriptNamesTranslateCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 3. BINARY PROPERTIES — targets remaining uncovered branches
    //    (Hex_Digit, Lowercase, Uppercase, ID_Start, Any, Assigned,
    //     Emoji_Presentation, Extended_Pictographic)
    // ================================================================

    [Theory]
    [InlineData(@"\p{Hex_Digit}", "A", true)]
    [InlineData(@"\p{Hex_Digit}", "f", true)]
    [InlineData(@"\p{Hex_Digit}", "G", false)]
    [InlineData(@"\p{Lowercase}", "a", true)]
    [InlineData(@"\p{Lowercase}", "A", false)]
    [InlineData(@"\p{Uppercase}", "A", true)]
    [InlineData(@"\p{Uppercase}", "a", false)]
    [InlineData(@"\p{ID_Start}", "a", true)]
    [InlineData(@"\p{ID_Start}", "5", false)]
    [InlineData(@"\p{Any}", "x", true)]
    [InlineData(@"\p{Assigned}", "a", true)]
    [InlineData(@"\p{Emoji_Presentation}", "\U0001F600", true)]  // 😀
    [InlineData(@"\p{Extended_Pictographic}", "\U0001F600", true)]
    public void BinaryPropertiesTranslateCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 4. SHORT CATEGORIES IN CHARACTER CLASSES — targets L430-436
    //    IsValidShortCategory is called via IsAlternationProperty when
    //    \p{Xx} appears in a [...] character class.
    // ================================================================

    [Theory]
    [InlineData(@"[\p{Mn}]+", "\u0300", true)]   // Nonspacing_Mark → L431
    [InlineData(@"[\p{Mn}]+", "a", false)]
    [InlineData(@"[\p{Mc}]+", "\u0903", true)]   // Spacing_Mark → L431
    [InlineData(@"[\p{Me}]+", "\u20DD", true)]   // Enclosing_Mark → L431
    [InlineData(@"[\p{Pc}]+", "_", true)]        // Connector_Punctuation → L433
    [InlineData(@"[\p{Pd}]+", "-", true)]        // Dash_Punctuation → L433
    [InlineData(@"[\p{Ps}]+", "(", true)]        // Open_Punctuation → L433
    [InlineData(@"[\p{Pe}]+", ")", true)]        // Close_Punctuation → L433
    [InlineData(@"[\p{Pi}]+", "\u00AB", true)]   // Initial_Punctuation → L433
    [InlineData(@"[\p{Pf}]+", "\u00BB", true)]   // Final_Punctuation → L433
    [InlineData(@"[\p{Po}]+", "!", true)]        // Other_Punctuation → L433
    [InlineData(@"[\p{Sm}]+", "+", true)]        // Math_Symbol → L434
    [InlineData(@"[\p{Sc}]+", "$", true)]        // Currency_Symbol → L434
    [InlineData(@"[\p{Sk}]+", "^", true)]        // Modifier_Symbol → L434
    [InlineData(@"[\p{So}]+", "\u00A9", true)]   // Other_Symbol → L434
    [InlineData(@"[\p{Zs}]+", " ", true)]        // Space_Separator → L435
    [InlineData(@"[\p{Zl}]+", "\u2028", true)]   // Line_Separator → L435
    [InlineData(@"[\p{Zp}]+", "\u2029", true)]   // Paragraph_Separator → L435
    [InlineData(@"[\p{Cc}]+", "\u0001", true)]   // Control → L436
    [InlineData(@"[\p{Cf}]+", "\u200B", true)]   // Format → L436
    [InlineData(@"[\p{Co}]+", "\uE000", true)]   // Private_Use → L436
    [InlineData(@"[\p{Cn}]+", "\u0378", true)]   // Unassigned → L436
    public void ShortCategoriesInCharClassTranslateCorrectly(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 5. TranslateOrFallback — targets L259-265 (catch blocks)
    // ================================================================

    [Fact]
    public void TranslateOrFallback_InvalidPattern_ReturnsFallback()
    {
        // An invalid ECMAScript regex pattern (e.g., lone \p with no braces)
        // should return the original pattern as fallback.
        string invalid = @"\p{InvalidCategoryThatDoesNotExist}";
        string result = EcmaRegexTranslator.TranslateOrFallback(invalid);
        Assert.Equal(invalid, result);
    }

    // ================================================================
    // 6. TryTranslate with too-small buffer — targets L232-233
    //    (OperationStatus.DestinationTooSmall)
    // ================================================================

    [Fact]
    public void TryTranslate_BufferTooSmall_ReturnsDestinationTooSmall()
    {
        // A pattern that expands (e.g., \s → whitespace class)
        // should fail with a 1-char buffer.
        Span<char> tinyBuffer = stackalloc char[1];
        OperationStatus status = EcmaRegexTranslator.TryTranslate(@"\s", tinyBuffer, out _);
        Assert.Equal(OperationStatus.DestinationTooSmall, status);
    }

    // ================================================================
    // 7. Supplementary code point in \u{XXXXX} form — uses lowercase hex
    //    Targets L353 (HexDigitValue lowercase 'a'-'f' branch)
    // ================================================================

    [Theory]
    [InlineData(@"\u{1f600}", "\U0001F600", true)]   // 😀 with lowercase hex
    [InlineData(@"\u{1f600}", "a", false)]
    public void LowercaseHexInCodePointEscapeWorks(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 8. Character class with \D, \W, \S (negated shorthand inside [...])
    //    Targets L689-698 (HasNegD, HasNegW, HasNegS scanning)
    // ================================================================

    [Theory]
    [InlineData(@"[\D]+", "abc", true)]        // \D in class → matches non-digits
    [InlineData(@"[\D]+", "123", false)]
    [InlineData(@"[\W]+", "!@#", true)]        // \W in class → matches non-word
    [InlineData(@"[\W]+", "abc", false)]
    [InlineData(@"[\S]+", "abc", true)]        // \S in class → matches non-space
    [InlineData(@"[\S]+", " \t", false)]
    public void NegatedShorthandsInCharClassTranslate(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 9. Character class with \xHH (hex escape inside class)
    //    Targets L755-758 scanning path
    // ================================================================

    [Theory]
    [InlineData(@"[\x41-\x5A]+", "ABC", true)]    // A-Z via hex escapes
    [InlineData(@"[\x41-\x5A]+", "abc", false)]
    public void HexEscapeInCharClassScanning(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 10. Character class with \cX (control escape inside class)
    //     Targets L759-762 scanning path
    // ================================================================

    [Theory]
    [InlineData(@"[\cA]+", "\u0001", true)]    // Control-A
    [InlineData(@"[\cA]+", "A", false)]
    public void ControlEscapeInCharClassScanning(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 11. Surrogate pair literal in character class
    //     Targets L772-780 (literal non-BMP char in [...])
    // ================================================================

    [Theory]
    [InlineData("[\U0001F600]", "\U0001F600", true)]   // 😀 literal in class
    [InlineData("[\U0001F600]", "a", false)]
    public void SurrogatePairLiteralInCharClass(string ecma, string input, bool shouldMatch)
    {
        AssertMatch(ecma, input, shouldMatch);
    }

    // ================================================================
    // 12. \p{...} inside character class with alternation property
    //     Targets L728-752 (scanning for alternation properties in class)
    //     and L811 (IsAlternationProperty returns false for non-script)
    // ================================================================

    [Theory]
    [InlineData(@"[\p{Emoji}a-z]+", "\U0001F600", true)]   // Emoji = alternation property
    [InlineData(@"[\p{Emoji}a-z]+", "abc", true)]
    [InlineData(@"[\p{Emoji}a-z]+", "\U0001F4A9", true)]  // 💩 — also emoji
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
        Assert.Equal(shouldMatch, matched);
    }
}
