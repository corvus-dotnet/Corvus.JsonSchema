// <copyright file="XPathDateTimeFormatterEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Edge-case tests for the XPath date/time formatting and integer formatting/parsing
/// functions, exercised via the JSONata $formatInteger, $parseInteger, $fromMillis, and $toMillis
/// built-in functions. Covers Roman numerals, word formats, ordinal suffixes, alphabetic encoding,
/// decimal digit patterns, picture string parsing, width modifiers, and datetime formatting.
/// </summary>
public class XPathDateTimeFormatterEdgeCaseTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    #region FormatInteger — Roman numerals

    [Theory]
    [InlineData(1, "\"I\"")]
    [InlineData(4, "\"IV\"")]
    [InlineData(9, "\"IX\"")]
    [InlineData(14, "\"XIV\"")]
    [InlineData(40, "\"XL\"")]
    [InlineData(90, "\"XC\"")]
    [InlineData(400, "\"CD\"")]
    [InlineData(900, "\"CM\"")]
    [InlineData(1999, "\"MCMXCIX\"")]
    [InlineData(3999, "\"MMMCMXCIX\"")]
    public void FormatInteger_UpperRoman(int value, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, 'I')", "{}");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, "\"i\"")]
    [InlineData(4, "\"iv\"")]
    [InlineData(2024, "\"mmxxiv\"")]
    public void FormatInteger_LowerRoman(int value, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, 'i')", "{}");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatInteger_RomanZero_ReturnsEmpty()
    {
        string? result = Evaluator.EvaluateToString("$formatInteger(0, 'I')", "{}");
        Assert.Equal("\"\"", result);
    }

    [Fact]
    public void FormatInteger_RomanNegative_ReturnsEmpty()
    {
        string? result = Evaluator.EvaluateToString("$formatInteger(-5, 'I')", "{}");
        Assert.Equal("\"\"", result);
    }

    #endregion

    #region ParseInteger — Roman numerals

    [Theory]
    [InlineData("I", 1)]
    [InlineData("IV", 4)]
    [InlineData("IX", 9)]
    [InlineData("XIV", 14)]
    [InlineData("XL", 40)]
    [InlineData("XC", 90)]
    [InlineData("CD", 400)]
    [InlineData("CM", 900)]
    [InlineData("MCMXCIX", 1999)]
    public void ParseInteger_UpperRoman(string input, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', 'I')", "{}");
        Assert.Equal(expected.ToString(), result);
    }

    [Theory]
    [InlineData("iv", 4)]
    [InlineData("mmxxiv", 2024)]
    public void ParseInteger_LowerRoman(string input, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', 'i')", "{}");
        Assert.Equal(expected.ToString(), result);
    }

    [Fact]
    public void ParseInteger_RomanEmpty_ReturnsZero()
    {
        string? result = Evaluator.EvaluateToString("$parseInteger('', 'I')", "{}");
        Assert.Equal("0", result);
    }

    #endregion

    #region FormatInteger — Words

    [Theory]
    [InlineData(0, "w", "\"zero\"")]
    [InlineData(1, "w", "\"one\"")]
    [InlineData(13, "w", "\"thirteen\"")]
    [InlineData(20, "w", "\"twenty\"")]
    [InlineData(21, "w", "\"twenty-one\"")]
    [InlineData(100, "w", "\"one hundred\"")]
    [InlineData(123, "w", "\"one hundred and twenty-three\"")]
    [InlineData(1000, "w", "\"one thousand\"")]
    [InlineData(1001, "w", "\"one thousand and one\"")]
    [InlineData(1234, "w", "\"one thousand, two hundred and thirty-four\"")]
    [InlineData(-5, "w", "\"minus five\"")]
    public void FormatInteger_Words_Lowercase(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(5, "W", "\"FIVE\"")]
    [InlineData(21, "W", "\"TWENTY-ONE\"")]
    public void FormatInteger_Words_Uppercase(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(5, "Ww", "\"Five\"")]
    [InlineData(21, "Ww", "\"Twenty-One\"")]
    [InlineData(100, "Ww", "\"One Hundred\"")]
    public void FormatInteger_Words_TitleCase(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.Equal(expected, result);
    }

    #endregion

    #region FormatInteger — Ordinal words

    [Theory]
    [InlineData(1, "\"first\"")]
    [InlineData(2, "\"second\"")]
    [InlineData(3, "\"third\"")]
    [InlineData(5, "\"fifth\"")]
    [InlineData(8, "\"eighth\"")]
    [InlineData(9, "\"ninth\"")]
    [InlineData(12, "\"twelfth\"")]
    [InlineData(20, "\"twentieth\"")]
    [InlineData(21, "\"twenty-first\"")]
    [InlineData(100, "\"one hundredth\"")]
    [InlineData(101, "\"one hundred and first\"")]
    public void FormatInteger_OrdinalWords(int value, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, 'w;o')", "{}");
        Assert.Equal(expected, result);
    }

    #endregion

    #region ParseInteger — Words

    [Theory]
    [InlineData("zero", 0)]
    [InlineData("one", 1)]
    [InlineData("thirteen", 13)]
    [InlineData("twenty", 20)]
    [InlineData("twenty-one", 21)]
    [InlineData("one hundred", 100)]
    [InlineData("one hundred and twenty-three", 123)]
    [InlineData("one thousand, two hundred and thirty-four", 1234)]
    public void ParseInteger_Words(string input, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', 'w')", "{}");
        Assert.Equal(expected.ToString(), result);
    }

    [Theory]
    [InlineData("first", 1)]
    [InlineData("second", 2)]
    [InlineData("third", 3)]
    [InlineData("twelfth", 12)]
    [InlineData("twentieth", 20)]
    [InlineData("twenty-first", 21)]
    public void ParseInteger_OrdinalWords(string input, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', 'w;o')", "{}");
        Assert.Equal(expected.ToString(), result);
    }

    #endregion

    #region FormatInteger — Alphabetic

    [Theory]
    [InlineData(1, "A", "\"A\"")]
    [InlineData(26, "A", "\"Z\"")]
    [InlineData(27, "A", "\"AA\"")]
    [InlineData(1, "a", "\"a\"")]
    [InlineData(26, "a", "\"z\"")]
    [InlineData(27, "a", "\"aa\"")]
    public void FormatInteger_Alphabetic(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatInteger_AlphabeticZero_ReturnsEmpty()
    {
        string? result = Evaluator.EvaluateToString("$formatInteger(0, 'A')", "{}");
        Assert.Equal("\"\"", result);
    }

    #endregion

    #region ParseInteger — Alphabetic

    [Theory]
    [InlineData("A", "A", 1)]
    [InlineData("Z", "A", 26)]
    [InlineData("AA", "A", 27)]
    [InlineData("a", "a", 1)]
    [InlineData("z", "a", 26)]
    public void ParseInteger_Alphabetic(string input, string picture, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', '{picture}')", "{}");
        Assert.Equal(expected.ToString(), result);
    }

    #endregion

    #region FormatInteger — Decimal digit patterns

    [Theory]
    [InlineData(42, "0", "\"42\"")]
    [InlineData(42, "00", "\"42\"")]
    [InlineData(42, "000", "\"042\"")]
    [InlineData(42, "0000", "\"0042\"")]
    [InlineData(7, "01", "\"07\"")]
    [InlineData(7, "1", "\"7\"")]
    public void FormatInteger_DecimalPattern_ZeroPadding(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, "0;o", "\"1st\"")]
    [InlineData(2, "0;o", "\"2nd\"")]
    [InlineData(3, "0;o", "\"3rd\"")]
    [InlineData(4, "0;o", "\"4th\"")]
    [InlineData(11, "0;o", "\"11th\"")]
    [InlineData(12, "0;o", "\"12th\"")]
    [InlineData(13, "0;o", "\"13th\"")]
    [InlineData(21, "0;o", "\"21st\"")]
    [InlineData(22, "0;o", "\"22nd\"")]
    [InlineData(23, "0;o", "\"23rd\"")]
    [InlineData(111, "0;o", "\"111th\"")]
    [InlineData(112, "0;o", "\"112th\"")]
    [InlineData(113, "0;o", "\"113th\"")]
    public void FormatInteger_OrdinalDecimal(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1234567, "0,000", "\"1,234,567\"")]
    [InlineData(1234, "0,000", "\"1,234\"")]
    [InlineData(123, "0,000", "\"0,123\"")]
    public void FormatInteger_GroupingSeparator(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.Equal(expected, result);
    }

    #endregion

    #region ParseInteger — Decimal patterns

    [Theory]
    [InlineData("042", "000", 42)]
    [InlineData("0042", "0000", 42)]
    [InlineData("7", "0", 7)]
    public void ParseInteger_DecimalPattern(string input, string picture, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', '{picture}')", "{}");
        Assert.Equal(expected.ToString(), result);
    }

    [Theory]
    [InlineData("1st", "0;o", 1)]
    [InlineData("2nd", "0;o", 2)]
    [InlineData("3rd", "0;o", 3)]
    [InlineData("11th", "0;o", 11)]
    [InlineData("21st", "0;o", 21)]
    public void ParseInteger_OrdinalDecimal(string input, string picture, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', '{picture}')", "{}");
        Assert.Equal(expected.ToString(), result);
    }

    [Theory]
    [InlineData("1,234,567", "0,000", 1234567)]
    [InlineData("1,234", "0,000", 1234)]
    public void ParseInteger_GroupingSeparator(string input, string picture, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', '{picture}')", "{}");
        Assert.Equal(expected.ToString(), result);
    }

    #endregion

    #region FormatInteger — Empty picture / fallback

    [Fact]
    public void FormatInteger_EmptyPicture_DefaultsToDecimal()
    {
        // Empty primary defaults to "0" pattern
        string? result = Evaluator.EvaluateToString("$formatInteger(42, ';o')", "{}");
        Assert.Equal("\"42nd\"", result);
    }

    [Fact]
    public void FormatInteger_SingleDigitPicture()
    {
        string? result = Evaluator.EvaluateToString("$formatInteger(5, '1')", "{}");
        Assert.Equal("\"5\"", result);
    }

    #endregion

    #region FormatInteger — Large values (double path)

    [Fact]
    public void FormatInteger_LargeDoubleAsWords()
    {
        // 1e18 fits in a long — formatter uses scale words up to "trillion"
        string? result = Evaluator.EvaluateToString("$formatInteger(1e18, 'w')", "{}");
        Assert.NotNull(result);
        Assert.Contains("trillion", result);
    }

    [Fact]
    public void FormatInteger_LargeDoubleAsDecimal()
    {
        // 1e18 fits in a long, so output is the full decimal representation
        string? result = Evaluator.EvaluateToString("$formatInteger(1e18, '0')", "{}");
        Assert.Equal("\"1000000000000000000\"", result);
    }

    #endregion

    #region FormatDateTime — Picture string edge cases

    [Theory]
    [InlineData("[Y0001]-[M01]-[D01]", "\"2021-04-07\"")]
    [InlineData("[Y]-[M]-[D]", "\"2021-4-7\"")]
    public void FormatDateTime_DatePictures(string picture, string expected)
    {
        // 2021-04-07T22:00:00.000Z = 1617836400000; pass explicit UTC timezone
        string? result = Evaluator.EvaluateToString($"$fromMillis(1617836400000, '{picture}', '+0000')", "{}");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatDateTime_TimePicture_WithTimezone()
    {
        // Use $fromMillis with explicit timezone offset
        // 1970-01-01T12:30:45.000Z = 45045000
        string? result = Evaluator.EvaluateToString("$fromMillis(45045000, '[H01]:[m01]:[s01]', '+0000')", "{}");
        Assert.NotNull(result);
        // The result should contain a valid HH:MM:SS time
        Assert.Matches("^\"\\d{2}:\\d{2}:\\d{2}\"$", result);
    }

    [Fact]
    public void FormatDateTime_LiteralText()
    {
        // Text between brackets is a component; text outside is literal
        string? result = Evaluator.EvaluateToString("$fromMillis(1617836400000, 'Date: [D01]/[M01]/[Y0001]', '+0000')", "{}");
        Assert.Equal("\"Date: 07/04/2021\"", result);
    }

    [Fact]
    public void FormatDateTime_EscapedBrackets()
    {
        // [[ and ]] are escaped brackets
        string? result = Evaluator.EvaluateToString("$fromMillis(1617836400000, '[[Year [Y0001]]]', '+0000')", "{}");
        Assert.Equal("\"[Year 2021]\"", result);
    }

    [Theory]
    [InlineData("[MNn]", "\"April\"")]
    [InlineData("[MN]", "\"APRIL\"")]
    [InlineData("[Mn]", "\"april\"")]
    public void FormatDateTime_MonthName_Casing(string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$fromMillis(1617836400000, '{picture}', '+0000')", "{}");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("[FNn]", "\"Wednesday\"")]
    [InlineData("[FN]", "\"WEDNESDAY\"")]
    [InlineData("[Fn]", "\"wednesday\"")]
    public void FormatDateTime_DayOfWeekName_Casing(string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$fromMillis(1617836400000, '{picture}', '+0000')", "{}");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatDateTime_InternalWhitespace_InPicture()
    {
        // Picture markers with internal whitespace should have whitespace stripped
        // [f0  01] is equivalent to [f001] — the standard requires stripping all whitespace
        // from the format token
        string? result = Evaluator.EvaluateToString("$fromMillis(1617836400000, '[Y 0001]-[M 01]-[D 01]', '+0000')", "{}");
        Assert.Equal("\"2021-04-07\"", result);
    }

    #endregion

    #region FormatDateTime — Width modifiers

    [Theory]
    [InlineData("[MNn,3-3]", "\"Apr\"")]
    [InlineData("[MNn,*-3]", "\"Apr\"")]
    [InlineData("[Y,4-4]", "\"2021\"")]
    public void FormatDateTime_WidthModifiers(string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$fromMillis(1617836400000, '{picture}', '+0000')", "{}");
        Assert.Equal(expected, result);
    }

    #endregion

    #region $toMillis — Parse round-trip

    [Theory]
    [InlineData("2000-01-01T00:00:00.000Z", 946684800000)]
    [InlineData("1970-01-01T00:00:00.000Z", 0)]
    public void ToMillis_ISO8601(string timestamp, long expectedMillis)
    {
        string? result = Evaluator.EvaluateToString($"$toMillis('{timestamp}')", "{}");
        Assert.Equal(expectedMillis.ToString(), result);
    }

    [Fact]
    public void FormatDateTime_FromMillis_RoundTrip()
    {
        // Format then parse back with explicit UTC timezone — should round-trip
        string? result = Evaluator.EvaluateToString(
            "$toMillis($fromMillis(946684800000, '[Y0001]-[M01]-[D01]T[H01]:[m01]:[s01].[f001]Z', '+0000'), '[Y0001]-[M01]-[D01]T[H01]:[m01]:[s01].[f001]Z')",
            "{}");
        Assert.Equal("946684800000", result);
    }

    #endregion

    #region $toMillis — Custom picture parse

    [Fact]
    public void ToMillis_CustomPicture()
    {
        // Use a date-only format (no timezone ambiguity for midnight UTC)
        string? result = Evaluator.EvaluateToString(
            "$toMillis('01/01/2000', '[D01]/[M01]/[Y0001]')",
            "{}");
        Assert.NotNull(result);
        // Parse returned a valid number
        Assert.True(long.TryParse(result, out _));
    }

    #endregion

    #region Error cases

    [Fact]
    public void FormatInteger_InvalidPicture_ThrowsD3130()
    {
        Assert.Throws<JsonataException>(() => Evaluator.EvaluateToString("$formatInteger(42, '#')", "{}"));
    }

    [Fact]
    public void FormatInteger_NamePresentation_ThrowsD3133()
    {
        // N/n/Nn presentations are invalid for $formatInteger
        Assert.Throws<JsonataException>(() => Evaluator.EvaluateToString("$formatInteger(42, 'N')", "{}"));
    }

    [Fact]
    public void FormatDateTime_UnmatchedBracket_ThrowsD3135()
    {
        Assert.Throws<JsonataException>(() => Evaluator.EvaluateToString("$fromMillis(1617836400000, '[Y0001')", "{}"));
    }

    #endregion

    #region Negative and zero values

    [Theory]
    [InlineData(0, "0", "\"0\"")]
    [InlineData(-1, "0", "\"-1\"")]
    [InlineData(-42, "0", "\"-42\"")]
    [InlineData(-123, "000", "\"-123\"")]
    public void FormatInteger_NegativeAndZero(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "w", "\"zero\"")]
    [InlineData(-1, "w", "\"minus one\"")]
    [InlineData(-100, "w", "\"minus one hundred\"")]
    public void FormatInteger_NegativeWords(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.Equal(expected, result);
    }

    #endregion

    #region Boundary values

    [Theory]
    [InlineData(999999999, "w")]
    [InlineData(1000000, "w")]
    [InlineData(1000000000, "w")]
    public void FormatInteger_LargeWordValues_DoNotThrow(int value, string picture)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.NotNull(result);
        Assert.StartsWith("\"", result);
    }

    [Theory]
    [InlineData(1, "0", "\"1\"")]
    [InlineData(9, "0", "\"9\"")]
    [InlineData(10, "0", "\"10\"")]
    [InlineData(99, "0", "\"99\"")]
    [InlineData(100, "0", "\"100\"")]
    [InlineData(999, "0", "\"999\"")]
    public void FormatInteger_PowerOf10Boundaries(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.Equal(expected, result);
    }

    #endregion
}
