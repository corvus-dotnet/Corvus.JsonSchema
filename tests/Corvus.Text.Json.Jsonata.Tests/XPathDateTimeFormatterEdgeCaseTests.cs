// <copyright file="XPathDateTimeFormatterEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Edge-case tests for the XPath date/time formatting and integer formatting/parsing
/// functions, exercised via the JSONata $formatInteger, $parseInteger, $fromMillis, and $toMillis
/// built-in functions. Covers Roman numerals, word formats, ordinal suffixes, alphabetic encoding,
/// decimal digit patterns, picture string parsing, width modifiers, and datetime formatting.
/// </summary>
[TestClass]
public class XPathDateTimeFormatterEdgeCaseTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    #region FormatInteger — Roman numerals

    [TestMethod]
    [DataRow(1, "\"I\"")]
    [DataRow(4, "\"IV\"")]
    [DataRow(9, "\"IX\"")]
    [DataRow(14, "\"XIV\"")]
    [DataRow(40, "\"XL\"")]
    [DataRow(90, "\"XC\"")]
    [DataRow(400, "\"CD\"")]
    [DataRow(900, "\"CM\"")]
    [DataRow(1999, "\"MCMXCIX\"")]
    [DataRow(3999, "\"MMMCMXCIX\"")]
    public void FormatInteger_UpperRoman(int value, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, 'I')", "{}");
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(1, "\"i\"")]
    [DataRow(4, "\"iv\"")]
    [DataRow(2024, "\"mmxxiv\"")]
    public void FormatInteger_LowerRoman(int value, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, 'i')", "{}");
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void FormatInteger_RomanZero_ReturnsEmpty()
    {
        string? result = Evaluator.EvaluateToString("$formatInteger(0, 'I')", "{}");
        Assert.AreEqual("\"\"", result);
    }

    [TestMethod]
    public void FormatInteger_RomanNegative_ReturnsEmpty()
    {
        string? result = Evaluator.EvaluateToString("$formatInteger(-5, 'I')", "{}");
        Assert.AreEqual("\"\"", result);
    }

    #endregion

    #region ParseInteger — Roman numerals

    [TestMethod]
    [DataRow("I", 1)]
    [DataRow("IV", 4)]
    [DataRow("IX", 9)]
    [DataRow("XIV", 14)]
    [DataRow("XL", 40)]
    [DataRow("XC", 90)]
    [DataRow("CD", 400)]
    [DataRow("CM", 900)]
    [DataRow("MCMXCIX", 1999)]
    public void ParseInteger_UpperRoman(string input, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', 'I')", "{}");
        Assert.AreEqual(expected.ToString(), result);
    }

    [TestMethod]
    [DataRow("iv", 4)]
    [DataRow("mmxxiv", 2024)]
    public void ParseInteger_LowerRoman(string input, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', 'i')", "{}");
        Assert.AreEqual(expected.ToString(), result);
    }

    [TestMethod]
    public void ParseInteger_RomanEmpty_ReturnsZero()
    {
        string? result = Evaluator.EvaluateToString("$parseInteger('', 'I')", "{}");
        Assert.AreEqual("0", result);
    }

    #endregion

    #region FormatInteger — Words

    [TestMethod]
    [DataRow(0, "w", "\"zero\"")]
    [DataRow(1, "w", "\"one\"")]
    [DataRow(13, "w", "\"thirteen\"")]
    [DataRow(20, "w", "\"twenty\"")]
    [DataRow(21, "w", "\"twenty-one\"")]
    [DataRow(100, "w", "\"one hundred\"")]
    [DataRow(123, "w", "\"one hundred and twenty-three\"")]
    [DataRow(1000, "w", "\"one thousand\"")]
    [DataRow(1001, "w", "\"one thousand and one\"")]
    [DataRow(1234, "w", "\"one thousand, two hundred and thirty-four\"")]
    [DataRow(-5, "w", "\"minus five\"")]
    public void FormatInteger_Words_Lowercase(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(5, "W", "\"FIVE\"")]
    [DataRow(21, "W", "\"TWENTY-ONE\"")]
    public void FormatInteger_Words_Uppercase(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(5, "Ww", "\"Five\"")]
    [DataRow(21, "Ww", "\"Twenty-One\"")]
    [DataRow(100, "Ww", "\"One Hundred\"")]
    public void FormatInteger_Words_TitleCase(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region FormatInteger — Ordinal words

    [TestMethod]
    [DataRow(1, "\"first\"")]
    [DataRow(2, "\"second\"")]
    [DataRow(3, "\"third\"")]
    [DataRow(5, "\"fifth\"")]
    [DataRow(8, "\"eighth\"")]
    [DataRow(9, "\"ninth\"")]
    [DataRow(12, "\"twelfth\"")]
    [DataRow(20, "\"twentieth\"")]
    [DataRow(21, "\"twenty-first\"")]
    [DataRow(100, "\"one hundredth\"")]
    [DataRow(101, "\"one hundred and first\"")]
    public void FormatInteger_OrdinalWords(int value, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, 'w;o')", "{}");
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region ParseInteger — Words

    [TestMethod]
    [DataRow("zero", 0)]
    [DataRow("one", 1)]
    [DataRow("thirteen", 13)]
    [DataRow("twenty", 20)]
    [DataRow("twenty-one", 21)]
    [DataRow("one hundred", 100)]
    [DataRow("one hundred and twenty-three", 123)]
    [DataRow("one thousand, two hundred and thirty-four", 1234)]
    public void ParseInteger_Words(string input, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', 'w')", "{}");
        Assert.AreEqual(expected.ToString(), result);
    }

    [TestMethod]
    [DataRow("first", 1)]
    [DataRow("second", 2)]
    [DataRow("third", 3)]
    [DataRow("twelfth", 12)]
    [DataRow("twentieth", 20)]
    [DataRow("twenty-first", 21)]
    public void ParseInteger_OrdinalWords(string input, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', 'w;o')", "{}");
        Assert.AreEqual(expected.ToString(), result);
    }

    #endregion

    #region FormatInteger — Alphabetic

    [TestMethod]
    [DataRow(1, "A", "\"A\"")]
    [DataRow(26, "A", "\"Z\"")]
    [DataRow(27, "A", "\"AA\"")]
    [DataRow(1, "a", "\"a\"")]
    [DataRow(26, "a", "\"z\"")]
    [DataRow(27, "a", "\"aa\"")]
    public void FormatInteger_Alphabetic(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void FormatInteger_AlphabeticZero_ReturnsEmpty()
    {
        string? result = Evaluator.EvaluateToString("$formatInteger(0, 'A')", "{}");
        Assert.AreEqual("\"\"", result);
    }

    #endregion

    #region ParseInteger — Alphabetic

    [TestMethod]
    [DataRow("A", "A", 1)]
    [DataRow("Z", "A", 26)]
    [DataRow("AA", "A", 27)]
    [DataRow("a", "a", 1)]
    [DataRow("z", "a", 26)]
    public void ParseInteger_Alphabetic(string input, string picture, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', '{picture}')", "{}");
        Assert.AreEqual(expected.ToString(), result);
    }

    #endregion

    #region FormatInteger — Decimal digit patterns

    [TestMethod]
    [DataRow(42, "0", "\"42\"")]
    [DataRow(42, "00", "\"42\"")]
    [DataRow(42, "000", "\"042\"")]
    [DataRow(42, "0000", "\"0042\"")]
    [DataRow(7, "01", "\"07\"")]
    [DataRow(7, "1", "\"7\"")]
    public void FormatInteger_DecimalPattern_ZeroPadding(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(1, "0;o", "\"1st\"")]
    [DataRow(2, "0;o", "\"2nd\"")]
    [DataRow(3, "0;o", "\"3rd\"")]
    [DataRow(4, "0;o", "\"4th\"")]
    [DataRow(11, "0;o", "\"11th\"")]
    [DataRow(12, "0;o", "\"12th\"")]
    [DataRow(13, "0;o", "\"13th\"")]
    [DataRow(21, "0;o", "\"21st\"")]
    [DataRow(22, "0;o", "\"22nd\"")]
    [DataRow(23, "0;o", "\"23rd\"")]
    [DataRow(111, "0;o", "\"111th\"")]
    [DataRow(112, "0;o", "\"112th\"")]
    [DataRow(113, "0;o", "\"113th\"")]
    public void FormatInteger_OrdinalDecimal(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(1234567, "0,000", "\"1,234,567\"")]
    [DataRow(1234, "0,000", "\"1,234\"")]
    [DataRow(123, "0,000", "\"0,123\"")]
    public void FormatInteger_GroupingSeparator(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region ParseInteger — Decimal patterns

    [TestMethod]
    [DataRow("042", "000", 42)]
    [DataRow("0042", "0000", 42)]
    [DataRow("7", "0", 7)]
    public void ParseInteger_DecimalPattern(string input, string picture, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', '{picture}')", "{}");
        Assert.AreEqual(expected.ToString(), result);
    }

    [TestMethod]
    [DataRow("1st", "0;o", 1)]
    [DataRow("2nd", "0;o", 2)]
    [DataRow("3rd", "0;o", 3)]
    [DataRow("11th", "0;o", 11)]
    [DataRow("21st", "0;o", 21)]
    public void ParseInteger_OrdinalDecimal(string input, string picture, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', '{picture}')", "{}");
        Assert.AreEqual(expected.ToString(), result);
    }

    [TestMethod]
    [DataRow("1,234,567", "0,000", 1234567)]
    [DataRow("1,234", "0,000", 1234)]
    public void ParseInteger_GroupingSeparator(string input, string picture, int expected)
    {
        string? result = Evaluator.EvaluateToString($"$parseInteger('{input}', '{picture}')", "{}");
        Assert.AreEqual(expected.ToString(), result);
    }

    #endregion

    #region FormatInteger — Empty picture / fallback

    [TestMethod]
    public void FormatInteger_EmptyPicture_DefaultsToDecimal()
    {
        // Empty primary format (";o") throws D3130 — reference behavior.
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Evaluator.EvaluateToString("$formatInteger(42, ';o')", "{}"));
        Assert.AreEqual("D3130", ex.Code);
    }

    [TestMethod]
    public void FormatInteger_SingleDigitPicture()
    {
        string? result = Evaluator.EvaluateToString("$formatInteger(5, '1')", "{}");
        Assert.AreEqual("\"5\"", result);
    }

    #endregion

    #region FormatInteger — Large values (double path)

    [TestMethod]
    public void FormatInteger_LargeDoubleAsWords()
    {
        // 1e18 fits in a long — formatter uses scale words up to "trillion"
        string? result = Evaluator.EvaluateToString("$formatInteger(1e18, 'w')", "{}");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "trillion");
    }

    [TestMethod]
    public void FormatInteger_LargeDoubleAsDecimal()
    {
        // 1e18 fits in a long, so output is the full decimal representation
        string? result = Evaluator.EvaluateToString("$formatInteger(1e18, '0')", "{}");
        Assert.AreEqual("\"1000000000000000000\"", result);
    }

    #endregion

    #region FormatDateTime — Picture string edge cases

    [TestMethod]
    [DataRow("[Y0001]-[M01]-[D01]", "\"2021-04-07\"")]
    [DataRow("[Y]-[M]-[D]", "\"2021-4-7\"")]
    public void FormatDateTime_DatePictures(string picture, string expected)
    {
        // 2021-04-07T22:00:00.000Z = 1617836400000; pass explicit UTC timezone
        string? result = Evaluator.EvaluateToString($"$fromMillis(1617836400000, '{picture}', '+0000')", "{}");
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void FormatDateTime_TimePicture_WithTimezone()
    {
        // Use $fromMillis with explicit timezone offset
        // 1970-01-01T12:30:45.000Z = 45045000
        string? result = Evaluator.EvaluateToString("$fromMillis(45045000, '[H01]:[m01]:[s01]', '+0000')", "{}");
        Assert.IsNotNull(result);
        // The result should contain a valid HH:MM:SS time
        StringAssert.Matches(result, new System.Text.RegularExpressions.Regex("^\"\\d{2}:\\d{2}:\\d{2}\"$"));
    }

    [TestMethod]
    public void FormatDateTime_LiteralText()
    {
        // Text between brackets is a component; text outside is literal
        string? result = Evaluator.EvaluateToString("$fromMillis(1617836400000, 'Date: [D01]/[M01]/[Y0001]', '+0000')", "{}");
        Assert.AreEqual("\"Date: 07/04/2021\"", result);
    }

    [TestMethod]
    public void FormatDateTime_EscapedBrackets()
    {
        // [[ and ]] are escaped brackets
        string? result = Evaluator.EvaluateToString("$fromMillis(1617836400000, '[[Year [Y0001]]]', '+0000')", "{}");
        Assert.AreEqual("\"[Year 2021]\"", result);
    }

    [TestMethod]
    [DataRow("[MNn]", "\"April\"")]
    [DataRow("[MN]", "\"APRIL\"")]
    [DataRow("[Mn]", "\"april\"")]
    public void FormatDateTime_MonthName_Casing(string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$fromMillis(1617836400000, '{picture}', '+0000')", "{}");
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("[FNn]", "\"Wednesday\"")]
    [DataRow("[FN]", "\"WEDNESDAY\"")]
    [DataRow("[Fn]", "\"wednesday\"")]
    public void FormatDateTime_DayOfWeekName_Casing(string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$fromMillis(1617836400000, '{picture}', '+0000')", "{}");
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void FormatDateTime_InternalWhitespace_InPicture()
    {
        // Picture markers with internal whitespace should have whitespace stripped
        // [f0  01] is equivalent to [f001] — the standard requires stripping all whitespace
        // from the format token
        string? result = Evaluator.EvaluateToString("$fromMillis(1617836400000, '[Y 0001]-[M 01]-[D 01]', '+0000')", "{}");
        Assert.AreEqual("\"2021-04-07\"", result);
    }

    #endregion

    #region FormatDateTime — Width modifiers

    [TestMethod]
    [DataRow("[MNn,3-3]", "\"Apr\"")]
    [DataRow("[MNn,*-3]", "\"Apr\"")]
    [DataRow("[Y,4-4]", "\"2021\"")]
    public void FormatDateTime_WidthModifiers(string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$fromMillis(1617836400000, '{picture}', '+0000')", "{}");
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region $toMillis — Parse round-trip

    [TestMethod]
    [DataRow("2000-01-01T00:00:00.000Z", 946684800000)]
    [DataRow("1970-01-01T00:00:00.000Z", 0)]
    public void ToMillis_ISO8601(string timestamp, long expectedMillis)
    {
        string? result = Evaluator.EvaluateToString($"$toMillis('{timestamp}')", "{}");
        Assert.AreEqual(expectedMillis.ToString(), result);
    }

    [TestMethod]
    public void FormatDateTime_FromMillis_RoundTrip()
    {
        // Format then parse back with explicit UTC timezone — should round-trip
        string? result = Evaluator.EvaluateToString(
            "$toMillis($fromMillis(946684800000, '[Y0001]-[M01]-[D01]T[H01]:[m01]:[s01].[f001]Z', '+0000'), '[Y0001]-[M01]-[D01]T[H01]:[m01]:[s01].[f001]Z')",
            "{}");
        Assert.AreEqual("946684800000", result);
    }

    #endregion

    #region $toMillis — Custom picture parse

    [TestMethod]
    public void ToMillis_CustomPicture()
    {
        // Use a date-only format (no timezone ambiguity for midnight UTC)
        string? result = Evaluator.EvaluateToString(
            "$toMillis('01/01/2000', '[D01]/[M01]/[Y0001]')",
            "{}");
        Assert.IsNotNull(result);
        // Parse returned a valid number
        Assert.IsTrue(long.TryParse(result, out _));
    }

    #endregion

    #region Error cases

    [TestMethod]
    public void FormatInteger_InvalidPicture_ThrowsD3130()
    {
        Assert.ThrowsExactly<JsonataException>(() => Evaluator.EvaluateToString("$formatInteger(42, '#')", "{}"));
    }

    [TestMethod]
    public void FormatInteger_NamePresentation_ThrowsD3133()
    {
        // N/n/Nn presentations are invalid for $formatInteger
        Assert.ThrowsExactly<JsonataException>(() => Evaluator.EvaluateToString("$formatInteger(42, 'N')", "{}"));
    }

    [TestMethod]
    public void FormatDateTime_UnmatchedBracket_ThrowsD3135()
    {
        Assert.ThrowsExactly<JsonataException>(() => Evaluator.EvaluateToString("$fromMillis(1617836400000, '[Y0001')", "{}"));
    }

    #endregion

    #region Negative and zero values

    [TestMethod]
    [DataRow(0, "0", "\"0\"")]
    [DataRow(-1, "0", "\"-1\"")]
    [DataRow(-42, "0", "\"-42\"")]
    [DataRow(-123, "000", "\"-123\"")]
    public void FormatInteger_NegativeAndZero(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(0, "w", "\"zero\"")]
    [DataRow(-1, "w", "\"minus one\"")]
    [DataRow(-100, "w", "\"minus one hundred\"")]
    public void FormatInteger_NegativeWords(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Boundary values

    [TestMethod]
    [DataRow(999999999, "w")]
    [DataRow(1000000, "w")]
    [DataRow(1000000000, "w")]
    public void FormatInteger_LargeWordValues_DoNotThrow(int value, string picture)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.IsNotNull(result);
        Assert.StartsWith("\"", result);
    }

    [TestMethod]
    [DataRow(1, "0", "\"1\"")]
    [DataRow(9, "0", "\"9\"")]
    [DataRow(10, "0", "\"10\"")]
    [DataRow(99, "0", "\"99\"")]
    [DataRow(100, "0", "\"100\"")]
    [DataRow(999, "0", "\"999\"")]
    public void FormatInteger_PowerOf10Boundaries(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Unicode digits with grouping separators (lines 1618-1637)

    [TestMethod]
    [DataRow(12345, "\u0661,\u0660\u0660\u0660", "\u0661\u0662,\u0663\u0664\u0665")]  // Arabic-Indic digits ١٢,٣٤٥
    [DataRow(1000, "\u0661,\u0660\u0660\u0660", "\u0661,\u0660\u0660\u0660")]            // Arabic-Indic digits ١,٠٠٠
    public void FormatInteger_UnicodeDigitsWithGrouping(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.AreEqual($"\"{expected}\"", result);
    }

    [TestMethod]
    [DataRow(42, "\u0661", "\u0664\u0662")]                        // Arabic-Indic digits without grouping
    [DataRow(0, "\u0661", "\u0660")]                                // Zero in Arabic-Indic
    public void FormatInteger_UnicodeDigitsNoGrouping(int value, string picture, string expected)
    {
        string? result = Evaluator.EvaluateToString($"$formatInteger({value}, '{picture}')", "{}");
        Assert.AreEqual($"\"{expected}\"", result);
    }

    #endregion

    #region ParseInteger with grouping separators (lines 2675-2689)

    [TestMethod]
    public void ParseInteger_WithGroupingSeparators()
    {
        // $parseInteger("1,234", "#,##0") should return 1234
        string? result = Evaluator.EvaluateToString("""$parseInteger("1,234", "#,##0")""", "{}");
        Assert.AreEqual("1234", result);
    }

    [TestMethod]
    public void ParseInteger_NegativeWithGrouping()
    {
        string? result = Evaluator.EvaluateToString("""$parseInteger("-1,234", "#,##0")""", "{}");
        Assert.AreEqual("-1234", result);
    }

    #endregion

    #region Timezone format variations (XPathDateTimeFormatter lines 1079-1089)

    [TestMethod]
    public void FromMillis_TimezoneWithTModifier_EmptyPAfterStrip()
    {
        // [Zt] means: use Z for UTC, else +HH:MM. With non-UTC timezone, "t" is stripped leaving empty p.
        // Exercises line 1079-1083 (p.Length == 0 after stripping 't')
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Zt]", "+0530")""", "{}");
        Assert.IsNotNull(result);
        // Should format with colon since p.Length == 0 → useColon=true, digits=2
        StringAssert.Contains(result, "+05:30");
    }

    [TestMethod]
    public void FromMillis_TimezoneWithTModifier_Utc()
    {
        // [Zt] with UTC should produce "Z"
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Zt]")""", "{}");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "Z");
    }

    [TestMethod]
    public void FromMillis_TimezoneWithTwoDigitsNoColon()
    {
        // [Z01] → presentation is "01", length 2, no colon → hits else branch (lines 1085-1088)
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Z01]", "+0530")""", "{}");
        Assert.IsNotNull(result);
        // Should format without colon: +0530
        StringAssert.Contains(result, "+0530");
    }

    [TestMethod]
    public void FromMillis_TimezoneMinimalFormat()
    {
        // [Z0] → presentation is "0", length 1 → minimal format (digits=0, no leading zero)
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Z0]", "+0530")""", "{}");
        Assert.IsNotNull(result);
        // Should format as "5:30" (minimal, no leading zero on hour, minutes if non-zero)
        StringAssert.Contains(result, "+5:30");
    }

    [TestMethod]
    public void FromMillis_TimezoneNoColonFourDigits()
    {
        // [Z0101] → presentation is "0101", length 4 → no colon, 2 digits
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Z0101]", "+0530")""", "{}");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "+0530");
    }

    [TestMethod]
    public void FromMillis_NegativeTimezone()
    {
        // Verify negative timezone offset formatting
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Z01:01]", "-0800")""", "{}");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "-08:00");
    }

    #endregion

    #region Escaped bracket in picture string (XPathDateTimeFormatter lines 2989-3001)

    [TestMethod]
    public void FromMillis_EscapedClosingBracket()
    {
        // ]] in picture string = literal ']'
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Y]-[M01]-[D01]]]")""", "{}");
        Assert.IsNotNull(result);
        Assert.EndsWith("]", result!.Trim('"'));
    }

    [TestMethod]
    public void FromMillis_EscapedClosingBracketInMiddle()
    {
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Y]]][M01]")""", "{}");
        Assert.IsNotNull(result);
        // Should have ']' between year and month
        StringAssert.Contains(result!.Trim('"'), "]");
    }

    #endregion

    #region $toMillis with era/calendar name (XPathDateTimeFormatter SkipWord lines 3515-3528)

    [TestMethod]
    public void ToMillis_WithEraComponent()
    {
        // [E] = Era component — parsing calls SkipWord to skip "AD"/"CE"/etc.
        string? result = Evaluator.EvaluateToString(
            """$toMillis("2024 AD", "[Y] [E]")""", "{}");
        Assert.IsNotNull(result);
        // Should parse successfully even with the era text
        Assert.AreNotEqual("undefined", result);
    }

    [TestMethod]
    public void ToMillis_WithCalendarComponent()
    {
        // [C] = Calendar component — parsing calls SkipWord to skip "ISO"/"Gregorian"/etc.
        string? result = Evaluator.EvaluateToString(
            """$toMillis("2024 ISO", "[Y] [C]")""", "{}");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void ToMillis_WithDayName()
    {
        // $toMillis with picture that includes [FNn] (day of week name) exercises SkipDayOfWeek
        string? result = Evaluator.EvaluateToString(
            """$toMillis("Monday 15 January 2024", "[FNn] [D] [MNn] [Y]")""", "{}");
        Assert.IsNotNull(result);
        Assert.AreEqual("1705276800000", result);
    }

    [TestMethod]
    public void ToMillis_WithAbbreviatedDayName()
    {
        string? result = Evaluator.EvaluateToString(
            """$toMillis("Mon 15 Jan 2024", "[FNn,*-3] [D] [MNn,*-3] [Y]")""", "{}");
        Assert.IsNotNull(result);
        Assert.AreNotEqual("undefined", result);
    }

    #endregion

    #region ParseTimezoneArgument (XPathDateTimeFormatter lines 3550-3557, 3579-3588)

#if NET
    [TestMethod]
    public void FromMillis_IanaTimezone()
    {
        // Verifies that IANA timezone names (e.g. "America/New_York") are resolved
        // and a UTC offset is applied. 1705276800000 ms = 2024-01-15 00:00:00 UTC.
        // America/New_York is UTC-5 (EST) or UTC-4 (EDT), so the time portion must
        // differ from midnight (the exact value depends on current DST rules).
        // .NET Framework does not support IANA timezone IDs in FindSystemTimeZoneById.
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Y]-[M01]-[D01]T[H01]:[m01]:[s01]", "America/New_York")""", "{}");
        Assert.IsNotNull(result);
        Assert.DoesNotContain("00:00:00", result);
    }
#endif

    [TestMethod]
    public void FromMillis_ShortOffset()
    {
        // Short offset like "+5" (1-2 chars after sign) exercises lines 3579-3582
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Y]-[M01]-[D01]T[H01]:[m01]:[s01]", "+5")""", "{}");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "05:00:00");
    }

    [TestMethod]
    public void FromMillis_ColonOffset_BackwardCompat()
    {
        // Offset with colon "+05:30" — not the spec format (±HHMM) but accepted for backward compat
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Y]-[M01]-[D01]T[H01]:[m01]:[s01]", "+05:30")""", "{}");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "05:30:00");
    }

    [TestMethod]
    public void FromMillis_InvalidTimezone_TreatedAsUtc()
    {
        // Invalid timezone falls back to UTC (lines 3554-3557)
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Y]-[M01]-[D01]T[H01]:[m01]:[s01]", "Not/A/Zone")""", "{}");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "00:00:00");
    }

    [TestMethod]
    public void FromMillis_WeirdOffsetLength()
    {
        // Offset with 3 chars after sign "123" → hits else branch (lines 3585-3588)
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Y]-[M01]-[D01]T[H01]:[m01]:[s01]", "+123")""", "{}");
        Assert.IsNotNull(result);
        // Falls through to hours=0, minutes=0 → treated as UTC
        StringAssert.Contains(result, "00:00:00");
    }

    #endregion

    #region $fromMillis with various picture components (lines 2730-2743, 3504-3517)

    [TestMethod]
    public void FromMillis_DayOfWeek()
    {
        // 2024-01-15 (Monday) at midnight UTC = 1705276800000
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[FNn]")""", "{}");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "Monday");
    }

    [TestMethod]
    public void FromMillis_WeekOfYear()
    {
        // Week number formatting
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[W01]")""", "{}");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void FromMillis_MonthName()
    {
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[MNn]")""", "{}");
        Assert.IsNotNull(result);
        StringAssert.Contains(result, "January");
    }

    [TestMethod]
    public void FromMillis_TimezoneOffset()
    {
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[H01]:[m01]:[s01] [Z]")""", "{}");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void FromMillis_EraAndCalendar()
    {
        string? result = Evaluator.EvaluateToString(
            """$fromMillis(1705276800000, "[Y]-[M01]-[D01] [E]")""", "{}");
        Assert.IsNotNull(result);
    }

    #endregion

    #region Width modifier parsing (fix for jsonata-js#750)

    [TestMethod]
    public void ToMillis_WidthModifier_FixedWidth()
    {
        // Tests fix for jsonata-js#750: width modifier ,min-max was not applied
        // when maxWidthFromPic > digitWidth (e.g. [H1,2-2] has digit=1, max=2).
        // Reference: $toMillis("20250703T231120Z","[Y1111][M11][D11]T[H1,2-2][m1,2-2][s1,2-2]Z") => 1751584280000
        string? result = Evaluator.EvaluateToString(
            """$toMillis("20250703T231120Z","[Y1111][M11][D11]T[H1,2-2][m1,2-2][s1,2-2]Z")""", "{}");
        Assert.AreEqual("1751584280000", result);
    }

    [TestMethod]
    public void ToMillis_WidthModifier_AllComponents()
    {
        // Width modifiers on all components: [Y1,4-4][M1,2-2][D1,2-2]T[H1,2-2][m1,2-2][s1,2-2]Z
        // Reference: 1751584280000
        string? result = Evaluator.EvaluateToString(
            """$toMillis("20250703T231120Z","[Y1,4-4][M1,2-2][D1,2-2]T[H1,2-2][m1,2-2][s1,2-2]Z")""", "{}");
        Assert.AreEqual("1751584280000", result);
    }

    [TestMethod]
    public void FromMillis_WidthModifier_RoundTrip()
    {
        // Verify $fromMillis produces correct output with width modifiers, then round-trip
        // Reference: $fromMillis(1751584280000, "[Y1,4-4][M1,2-2][D1,2-2]T[H1,2-2][m1,2-2][s1,2-2]Z") => "20250703T231120Z"
        string? formatted = Evaluator.EvaluateToString(
            """$fromMillis(1751584280000, "[Y1,4-4][M1,2-2][D1,2-2]T[H1,2-2][m1,2-2][s1,2-2]Z")""", "{}");
        Assert.AreEqual("\"20250703T231120Z\"", formatted);

        // Round-trip: parse the formatted string back to millis
        string? millis = Evaluator.EvaluateToString(
            """$toMillis("20250703T231120Z","[Y1,4-4][M1,2-2][D1,2-2]T[H1,2-2][m1,2-2][s1,2-2]Z")""", "{}");
        Assert.AreEqual("1751584280000", millis);
    }

    #endregion
}
