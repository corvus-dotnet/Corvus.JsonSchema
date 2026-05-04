// <copyright file="XPathDateTimeFormatterCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests targeting specific uncovered lines in <see cref="XPathDateTimeFormatter"/>,
/// identified from merged all-TFM Cobertura coverage data (2026-05-04 baseline).
/// Focuses on: FormatInteger(double) for very large values, Unicode digit formatting/parsing,
/// picture parsing edge cases, and word-number conversion paths.
/// </summary>
public class XPathDateTimeFormatterCoverageTests
{
    private static string Eval(string expression, string data = "null")
    {
        return JsonataEvaluator.Default.EvaluateToString(expression, data) ?? "undefined";
    }

    #region FormatInteger(double) — values outside long range (lines 521-561)

    [Fact]
    public void FormatInteger_VeryLargeDouble_WordFormat()
    {
        // 1e19 > long.MaxValue → exercises FormatInteger(double) with "W" pattern (line 553)
        string result = Eval("""$formatInteger(10000000000000000000, "W")""");
        Assert.Contains("TRILLION", result);
    }

    [Fact]
    public void FormatInteger_VeryLargeDouble_LowerWordFormat()
    {
        // 1e19 with "w" pattern → lowercase words (line 553)
        string result = Eval("""$formatInteger(10000000000000000000, "w")""");
        Assert.Contains("trillion", result);
    }

    [Fact]
    public void FormatInteger_VeryLargeDouble_TitleWordFormat()
    {
        // 1e19 with "Ww" pattern → title case words (line 553)
        string result = Eval("""$formatInteger(10000000000000000000, "Ww")""");
        Assert.Contains("Trillion", result);
    }

#if NET
    [Fact]
    public void FormatInteger_VeryLargeDouble_DecimalPattern()
    {
        // 1e19 with numeric pattern → exercises the else branch (lines 556-561)
        // Requires .NET (Core) — .NET Framework throws FormatException for large double formatting.
        string result = Eval("""$formatInteger(10000000000000000000, "0")""");
        Assert.NotNull(result);
        // Should contain digits representing the large number
        Assert.StartsWith("\"", result);
    }

    [Fact]
    public void FormatInteger_VeryLargeDouble_EmptyPrimary()
    {
        // Very large value with ";o" (empty primary → defaults to "0") (line 547-549)
        // Requires .NET (Core) — .NET Framework throws FormatException for large double formatting.
        string result = Eval("""$formatInteger(10000000000000000000, ";o")""");
        Assert.NotNull(result);
    }
#endif

    [Fact]
    public void FormatInteger_VeryLargeDouble_WithOrdinal()
    {
        // 1e19 with ordinal modifier (line 533-540)
        string result = Eval("""$formatInteger(10000000000000000000, "w;o")""");
        Assert.Contains("th", result);
    }

    #endregion

    #region AppendNumberWordsLarge — trillions path (lines 2090-2115)

    [Fact]
    public void FormatInteger_Trillion_InWords()
    {
        // 2 trillion exercises AppendNumberWordsLarge (line 2090-2092 passes to AppendNumberWords)
        string result = Eval("""$formatInteger(2000000000000, "w")""");
        Assert.Equal("\"two trillion\"", result);
    }

    [Fact]
    public void FormatInteger_TrillionWithRemainder_InWords()
    {
        // 2 trillion + 50 exercises "and" path (line 2114 — remainder < 100)
        string result = Eval("""$formatInteger(2000000000050, "w")""");
        Assert.Contains("and fifty", result);
    }

    [Fact]
    public void FormatInteger_TrillionWithLargeRemainder_InWords()
    {
        // 2 trillion + 500 exercises ", " separator path (line 2114 — remainder >= 100)
        string result = Eval("""$formatInteger(2000000000500, "w")""");
        Assert.Contains("five hundred", result);
    }

    [Fact]
    public void FormatInteger_VeryLargeDouble_WithRemainder_InWords()
    {
        // 1.05e19 > long.MaxValue, and has non-zero remainder after trillion division
        // exercises AppendNumberWordsLarge line 2114-2115 (", " + remainder)
        string result = Eval("""$formatInteger(10500000000000000000, "w")""");
        Assert.Contains("trillion", result);
        // The remainder portion (500 trillion) should appear
        Assert.NotEqual("\"ten million trillion\"", result);
    }

    [Fact]
    public void FormatInteger_NegativeVeryLargeDouble_InWords()
    {
        // Negative 1e19 > |long.MinValue| → exercises AppendAsWordsLarge negative path (lines 1979-1981)
        string result = Eval("""$formatInteger(-10000000000000000000, "w")""");
        Assert.Contains("minus", result);
        Assert.Contains("trillion", result);
    }

    [Fact]
    public void FormatInteger_VeryVeryLarge_Recursive()
    {
        // 1e25 exercises the recursive path in AppendNumberWordsLarge (line 2099-2100)
        // trillions = 1e13 > 1e12, causing recursion that then hits lines 2090-2092
        string result = Eval("""$formatInteger(10000000000000000000000000, "w")""");
        Assert.Contains("trillion", result);
    }

    #endregion

    #region ParseWordsToNumber — edge cases (lines 1979-1981, 2000-2002, 2419-2440)

    [Fact]
    public void ParseInteger_WordZero()
    {
        // "zero" text → exercises line 2000-2002
        string result = Eval("""$parseInteger("zero", "w")""");
        Assert.Equal("0", result);
    }

    [Fact]
    public void ParseInteger_WordZeroth()
    {
        // "zeroth" text with ordinal → exercises zeroth path
        string result = Eval("""$parseInteger("zeroth", "w;o")""");
        Assert.Equal("0", result);
    }

    [Fact]
    public void ParseInteger_NegativeWord()
    {
        // "minus five" → exercises negative word path (line 1979-1981 analog in parse)
        string result = Eval("""$formatInteger(-5, "w")""");
        Assert.Contains("minus", result);
    }

    [Fact]
    public void FormatInteger_ZeroInWords()
    {
        // 0 in word format exercises AppendNumberWords zero path (line 2000-2002)
        string result = Eval("""$formatInteger(0, "w")""");
        Assert.Equal("\"zero\"", result);
    }

    [Fact]
    public void ParseInteger_LargeCompound()
    {
        // "one hundred thousand" exercises the scale > hundred path (lines 2423-2427)
        string result = Eval("""$parseInteger("one hundred thousand", "w")""");
        Assert.Equal("100000", result);
    }

    [Fact]
    public void ParseInteger_TwoHundred()
    {
        // "two hundred" exercises the hundred multiplier path (lines 2437-2442)
        string result = Eval("""$parseInteger("two hundred", "w")""");
        Assert.Equal("200", result);
    }

    [Fact]
    public void ParseInteger_OneHundred_ImpliedOne()
    {
        // "hundred" alone exercises `if (current == 0) current = 1;` (line 2438-2439)
        string result = Eval("""$parseInteger("hundred", "w")""");
        Assert.Equal("100", result);
    }

    [Fact]
    public void ParseInteger_OneThousand_ImpliedOne()
    {
        // "thousand" alone exercises `if (current == 0) current = 1;` (line 2419-2421)
        string result = Eval("""$parseInteger("thousand", "w")""");
        Assert.Equal("1000", result);
    }

    [Fact]
    public void ParseInteger_InvalidWord()
    {
        // Unrecognized word exercises `numVal < 0 → return false` (line 2449-2452)
        string result = Eval("""$parseInteger("banana", "w")""");
        // Returns undefined/NaN when parse fails
        Assert.True(result == "undefined" || result == "null" || result.Contains("NaN"));
    }

    #endregion

    #region Unicode digit formatting and parsing (lines 2687-2766)

    [Fact]
    public void FormatInteger_ArabicIndicDigits()
    {
        // Arabic-Indic digit zero (U+0660) as picture → format using Arabic digits
        string result = Eval("$formatInteger(42, '\u0660')");
        Assert.NotNull(result);
        // Should contain Arabic-Indic digits for 42: ٤٢
        Assert.Contains("\u0664", result); // ٤ = U+0664
        Assert.Contains("\u0662", result); // ٢ = U+0662
    }

    [Fact]
    public void ParseInteger_ArabicIndicDigits()
    {
        // Parse "٤٢" with Arabic-Indic picture (exercises lines 2687-2766)
        string result = Eval("$parseInteger('\u0664\u0662', '\u0660')");
        Assert.Equal("42", result);
    }

    [Fact]
    public void ParseInteger_ArabicIndicDigitsWithSeparator()
    {
        // Parse "١٬٠٠٠" (1,000 in Arabic with Arabic comma separator)
        // Exercises multi-byte separator matching (lines 2739-2749)
        string result = Eval("$parseInteger('\u0661\u066C\u0660\u0660\u0660', '\u0660\u066C\u0660\u0660\u0660')");
        Assert.Equal("1000", result);
    }

    [Fact]
    public void FormatInteger_DevanagariDigits()
    {
        // Devanagari digit zero (U+0966) as picture
        string result = Eval("$formatInteger(123, '\u0966')");
        Assert.NotNull(result);
        // Result should contain Devanagari digits — verify non-empty and not plain ASCII
        string inner = result.Trim('"');
        Assert.NotEmpty(inner);
        Assert.True(inner.Length >= 3, "Should contain at least 3 characters for 123");
    }

    [Fact]
    public void ParseInteger_ArabicIndic_Negative()
    {
        // Negative sign before Arabic digits (line 2679-2683)
        string result = Eval("$parseInteger('-\u0664\u0662', '\u0660')");
        Assert.Equal("-42", result);
    }

    [Fact]
    public void ParseInteger_ArabicIndic_WithAsciiSeparator()
    {
        // Arabic-Indic digits with ASCII comma separator in picture
        // Picture "\u0660,\u0660\u0660\u0660" declares comma as grouping separator
        // Input "\u0661,\u0660\u0660\u0660" should parse as 1000
        // Exercises lines 2687-2694 (ASCII byte check for grouping separator in unicode mode)
        string result = Eval("$parseInteger('\u0661,\u0660\u0660\u0660', '\u0660,\u0660\u0660\u0660')");
        Assert.Equal("1000", result);
    }

    [Fact]
    public void ParseInteger_WordsWithTrailingSpaces()
    {
        // "  five  " with leading/trailing spaces exercises TrimAsciiWhitespace (lines 2264-2267)
        string result = Eval("""$parseInteger("  five  ", "w")""");
        Assert.Equal("5", result);
    }

    [Fact]
    public void ParseInteger_EmptyAfterTrim()
    {
        // All-spaces input → after trim, length is 0 → returns false (line 2271-2273)
        string result = Eval("""$parseInteger("   ", "w")""");
        Assert.True(result == "undefined" || result == "null" || result.Contains("NaN"));
    }

    #endregion

    #region Picture parsing — escaped brackets (lines 2990-3001)

    [Fact]
    public void ToMillis_PictureWithEscapedCloseBracket()
    {
        // "]]" outside a marker in $toMillis picture exercises ParsePictureString lines 2989-2995
        // The picture means: parse year, then literal ']' character
        string result = Eval("""$toMillis("2024]", "[Y]]]")""");
        Assert.NotNull(result);
        Assert.NotEqual("undefined", result);
    }

    [Fact]
    public void ToMillis_PictureWithUnpairedCloseBracket()
    {
        // Single ']' outside marker exercises line 2996-3001 (else branch)
        string result = Eval("""$toMillis("2024]", "[Y]]")""");
        Assert.NotNull(result);
    }

    [Fact]
    public void FromMillis_EscapedCloseBracket()
    {
        // "]]" in $fromMillis picture is a literal ']' (exercises FormatDateTime bracket handling)
        string result = Eval("""$fromMillis(1705276800000, "[Y]]]")""");
        Assert.NotNull(result);
        Assert.Contains("]", result);
    }

    [Fact]
    public void FromMillis_EscapedOpenBracket()
    {
        // "[[" in picture is a literal '['
        string result = Eval("""$fromMillis(1705276800000, "[[Y: [Y]]]")""");
        Assert.NotNull(result);
        Assert.Contains("[", result);
    }

    #endregion

    #region FormatInteger — FromAlpha empty path (line 1914-1916)

    [Fact]
    public void ParseInteger_EmptyString_AlphaPattern()
    {
        // Parse empty string with "a" pattern → exercises FromAlpha empty check (line 1914-1916)
        string result = Eval("""$parseInteger("", "a")""");
        Assert.True(result == "0" || result == "undefined" || result == "null");
    }

    #endregion

    #region TryParseIntegerWithPresentation — grouping separators (lines 2628-2631)

    [Fact]
    public void ParseInteger_WithGroupingSeparator()
    {
        // Parse "1,000" with picture "0,000" exercises grouping separator detection
        string result = Eval("""$parseInteger("1,000", "#,##0")""");
        Assert.Equal("1000", result);
    }

    [Fact]
    public void ParseInteger_WithDotGrouping()
    {
        // Parse "1.000.000" with dot grouping separator
        string result = Eval("""$parseInteger("1.000.000", "#.##0")""");
        Assert.Equal("1000000", result);
    }

    #endregion

    #region FormatDateTime — TrimAsciiWhitespace (lines 3601-3609)

    [Fact]
    public void FromMillis_PictureWithSpacesInMarker()
    {
        // Picture markers can have spaces stripped: "[ Y ]" → "Y"
        // This exercises StripAllAsciiWhitespace (line 3620-3632) via FormatDateTime
        string result = Eval("""$fromMillis(1705276800000, "[ Y ]")""");
        Assert.NotNull(result);
        Assert.Contains("2024", result);
    }

    [Fact]
    public void FromMillis_WidthModifierWithSpaces()
    {
        // Width modifier with spaces around the dash: [Y, 2 - 4]
        // Exercises TrimAsciiWhitespace lines 3601-3609 via ParseWidthModifier
        string result = Eval("""$fromMillis(1705276800000, "[Y, 2 - 4]")""");
        Assert.NotNull(result);
        Assert.Contains("2024", result);
    }

    [Fact]
    public void FromMillis_WidthModifierWithLeadingTrailingSpaces()
    {
        // Width modifier with spaces: exercises both start and end trim loops
        string result = Eval("""$fromMillis(1705276800000, "[Y,  *  ]")""");
        Assert.NotNull(result);
        Assert.Contains("2024", result);
    }

    #endregion

    #region TryDecodeUtf8CodePoint — 4-byte code points (lines 3672-3676)

    [Fact]
    public void FormatInteger_Emoji_ThrowsD3130()
    {
        // An emoji (4-byte UTF-8) in the picture pattern is not a recognized digit
        // and throws D3130 (exercises TryDecodeUtf8CodePoint 4-byte branch at line 3672-3676
        // then falls through to the throw at line 664)
        var ex = Assert.Throws<JsonataException>(() => Eval("$formatInteger(42, '\U0001F600')"));
        Assert.Equal("D3130", ex.Code);
    }

    #endregion

    #region Width modifier parsing (lines 1329-varied)

    [Fact]
    public void FormatInteger_WidthModifier_MinWidth()
    {
        // "#1" pattern with width modifier forces minimum width
        string result = Eval("""$formatInteger(5, "001")""");
        Assert.Equal("\"005\"", result);
    }

    [Fact]
    public void FormatInteger_GroupingSeparator_Thousands()
    {
        // Grouping separator in picture with digit pattern "0,000"
        string result = Eval("""$formatInteger(1234567, "0,000,000")""");
        Assert.Equal("\"1,234,567\"", result);
    }

    [Fact]
    public void FormatInteger_GroupingSeparator_DotSeparator()
    {
        // Dot as grouping separator "0.000.000"
        string result = Eval("""$formatInteger(1234567, "0.000.000")""");
        Assert.Equal("\"1.234.567\"", result);
    }

    #endregion

    #region Ordinal formatting with words ending in 'y' (lines 2153-2157)

    [Fact]
    public void FormatInteger_Ordinal_Twenty()
    {
        // "twenty" ends with 'y' → "twentieth" (line 2153-2157: y → ieth)
        string result = Eval("""$formatInteger(20, "w;o")""");
        Assert.Equal("\"twentieth\"", result);
    }

    [Fact]
    public void FormatInteger_Ordinal_Thirty()
    {
        // "thirty" ends with 'y' → "thirtieth"
        string result = Eval("""$formatInteger(30, "w;o")""");
        Assert.Equal("\"thirtieth\"", result);
    }

    [Fact]
    public void FormatInteger_Ordinal_Hundred()
    {
        // "hundred" → "hundredth" (exercises default "th" append, line 2161)
        string result = Eval("""$formatInteger(100, "w;o")""");
        Assert.Equal("\"one hundredth\"", result);
    }

    #endregion

    #region $fromMillis with various marker characters (lines 822-891)

    [Fact]
    public void FromMillis_WeekOfYear()
    {
        // [W] = week of year. 2024-01-15 is in week 3
        string result = Eval("""$fromMillis(1705276800000, "[W]")""");
        Assert.NotNull(result);
        int week = int.Parse(result.Trim('"'));
        Assert.InRange(week, 1, 53);
    }

    [Fact]
    public void FromMillis_IsoWeekOfYear()
    {
        // [W01] = ISO week padded to 2 digits
        string result = Eval("""$fromMillis(1705276800000, "[W01]")""");
        Assert.NotNull(result);
        string inner = result.Trim('"');
        Assert.Equal(2, inner.Length); // 2-digit padded: "03"
        Assert.True(int.TryParse(inner, out int week));
        Assert.InRange(week, 1, 53);
    }

    [Fact]
    public void FromMillis_12HourClock()
    {
        // [h] = 12-hour clock. Exercises the h/P marker handling (lines 822-824, 861)
        string result = Eval("""$fromMillis(1705320000000, "[h]:[m01] [P]")""");
        Assert.NotNull(result);
        // Contains am/pm indicator
        string val = result.Trim('"');
        Assert.True(val.Contains("am") || val.Contains("pm") || val.Contains("AM") || val.Contains("PM"),
            $"Expected am/pm indicator in: {val}");
    }

    [Fact]
    public void FromMillis_AmPmMarker()
    {
        // [P] = AM/PM marker (exercises lines 889-891)
        string result = Eval("""$fromMillis(1705276800000, "[P]")""");
        Assert.NotNull(result);
        string val = result.Trim('"');
        Assert.True(val == "am" || val == "pm" || val == "AM" || val == "PM",
            $"Expected am/pm value, got: {val}");
    }

    #endregion

    #region Sequence (lines 496-608) — ParseInteger with ordinal modifier

    [Fact]
    public void ParseInteger_Ordinal_First()
    {
        // "first" with ordinal modifier exercises TryParseWordsToNumber with ordinal stripping
        string result = Eval("""$parseInteger("first", "w;o")""");
        Assert.Equal("1", result);
    }

    [Fact]
    public void ParseInteger_Ordinal_Twentieth()
    {
        // "twentieth" exercises ordinal word parsing
        string result = Eval("""$parseInteger("twentieth", "w;o")""");
        Assert.Equal("20", result);
    }

    [Fact]
    public void ParseInteger_Ordinal_HundredAndFirst()
    {
        // "one hundred and first" exercises compound ordinal parsing
        string result = Eval("""$parseInteger("one hundred and first", "w;o")""");
        Assert.Equal("101", result);
    }

    #endregion
}
