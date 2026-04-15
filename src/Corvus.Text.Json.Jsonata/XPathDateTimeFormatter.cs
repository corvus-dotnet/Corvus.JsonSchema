// <copyright file="XPathDateTimeFormatter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Corvus.Text;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Formats and parses dates/times using XPath 3.1 picture strings, and formats/parses integers
/// using XPath integer picture strings. Used by the JSONata $fromMillis, $toMillis, $formatInteger,
/// and $parseInteger built-in functions.
/// </summary>
internal static class XPathDateTimeFormatter
{
    private static readonly string[] MonthNames =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    };

    private static readonly string[] DayNames =
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
    };

    private static readonly string[] Ones =
    {
        string.Empty, "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
        "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    };

    private static readonly string[] Tens =
    {
        string.Empty, string.Empty, "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety",
    };

    private static readonly Dictionary<string, string> OrdinalWordMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "one", "first" },
        { "two", "second" },
        { "three", "third" },
        { "four", "fourth" },
        { "five", "fifth" },
        { "six", "sixth" },
        { "seven", "seventh" },
        { "eight", "eighth" },
        { "nine", "ninth" },
        { "ten", "tenth" },
        { "eleven", "eleventh" },
        { "twelve", "twelfth" },
        { "thirteen", "thirteenth" },
        { "fourteen", "fourteenth" },
        { "fifteen", "fifteenth" },
        { "sixteen", "sixteenth" },
        { "seventeen", "seventeenth" },
        { "eighteen", "eighteenth" },
        { "nineteen", "nineteenth" },
        { "twenty", "twentieth" },
        { "thirty", "thirtieth" },
        { "forty", "fortieth" },
        { "fifty", "fiftieth" },
        { "sixty", "sixtieth" },
        { "seventy", "seventieth" },
        { "eighty", "eightieth" },
        { "ninety", "ninetieth" },
        { "hundred", "hundredth" },
        { "thousand", "thousandth" },
        { "million", "millionth" },
        { "billion", "billionth" },
        { "trillion", "trillionth" },
    };

    private static readonly (string Name, long Value)[] ScaleWords =
    {
        ("trillion", 1_000_000_000_000L),
        ("billion", 1_000_000_000L),
        ("million", 1_000_000L),
        ("thousand", 1_000L),
        ("hundred", 100L),
    };

    /// <summary>
    /// Formats a <see cref="DateTimeOffset"/> using an XPath picture string.
    /// </summary>
    /// <param name="dt">The date/time to format.</param>
    /// <param name="picture">The XPath picture string.</param>
    /// <returns>The formatted string.</returns>
    public static string FormatDateTime(DateTimeOffset dt, string picture)
    {
        Utf8ValueStringBuilder sb = new(stackalloc byte[256]);
        FormatDateTime(dt, picture, ref sb);
#if NET
        string result = Encoding.UTF8.GetString(sb.AsSpan());
#else
        string result = Encoding.UTF8.GetString(sb.AsSpan().ToArray());
#endif
        sb.Dispose();
        return result;
    }

    /// <summary>
    /// Formats the given <see cref="DateTimeOffset"/> using the XPath picture string,
    /// writing the UTF-8 result directly to a caller-supplied buffer.
    /// </summary>
    /// <param name="dt">The date and time to format.</param>
    /// <param name="picture">The XPath picture string.</param>
    /// <param name="destination">The destination buffer for UTF-8 output.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the destination was large enough; otherwise <see langword="false"/>.</returns>
    internal static bool TryFormatDateTime(DateTimeOffset dt, string picture, Span<byte> destination, out int bytesWritten)
    {
        Utf8ValueStringBuilder sb = new(destination);
        FormatDateTime(dt, picture, ref sb);
        bool success = sb.TryCopyTo(destination, out bytesWritten);
        sb.Dispose();
        return success;
    }

    internal static void FormatDateTime(DateTimeOffset dt, string picture, ref Utf8ValueStringBuilder sb)
    {
        ValidateBrackets(picture);

        int i = 0;

        while (i < picture.Length)
        {
            if (picture[i] == '[')
            {
                if (i + 1 < picture.Length && picture[i + 1] == '[')
                {
                    sb.Append((byte)'[');
                    i += 2;
                    continue;
                }

                int end = picture.IndexOf(']', i + 1);
                if (end < 0)
                {
                    throw new JsonataException("D3135", SR.D3135_PictureStringContainsAWithNoMatching, 0);
                }

                string marker = picture.Substring(i + 1, end - i - 1);
                string stripped = StripWhitespace(marker);
                FormatComponent(dt, stripped, ref sb);
                i = end + 1;
            }
            else if (picture[i] == ']')
            {
                if (i + 1 < picture.Length && picture[i + 1] == ']')
                {
                    sb.Append((byte)']');
                    i += 2;
                }
                else
                {
                    sb.Append((byte)']');
                    i++;
                }
            }
            else
            {
                sb.Append((byte)picture[i]);
                i++;
            }
        }
    }

    private static void ValidateBrackets(string picture)
    {
        int i = 0;
        while (i < picture.Length)
        {
            if (picture[i] == '[')
            {
                if (i + 1 < picture.Length && picture[i + 1] == '[')
                {
                    i += 2;
                    continue;
                }

                int end = picture.IndexOf(']', i + 1);
                if (end < 0)
                {
                    throw new JsonataException("D3135", SR.D3135_PictureStringContainsAWithNoMatching, 0);
                }

                i = end + 1;
            }
            else
            {
                i++;
            }
        }
    }

    /// <summary>
    /// Parses a date/time string using an XPath picture string and returns UTC milliseconds since epoch.
    /// </summary>
    /// <param name="str">The date/time string to parse.</param>
    /// <param name="picture">The XPath picture string.</param>
    /// <param name="millis">The parsed milliseconds since Unix epoch.</param>
    /// <returns><see langword="true"/> if the string was successfully parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParseDateTime(string str, string picture, out long millis)
    {
        millis = 0;

        var components = ParsePictureString(picture);

        int year = -1, month = -1, day = -1, hour = -1, minute = -1, second = -1, millisecond = -1;
        int dayOfYear = -1;
        int? tzOffsetMinutes = null;
        int pos = 0;

        foreach (var comp in components)
        {
            if (comp.IsLiteral)
            {
                if (pos + comp.Literal!.Length > str.Length)
                {
                    return false;
                }

                // Match literal text
                string expected = comp.Literal;
                string actual = str.Substring(pos, expected.Length);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                pos += expected.Length;
                continue;
            }

            char compChar = comp.Component;
            string presentation = comp.Presentation;
            int digitWidth = comp.DigitWidth;

            switch (compChar)
            {
                case 'Y':
                    year = ParseIntegerValueFromString(str, ref pos, presentation, digitWidth);
                    if (year < 0)
                    {
                        return false;
                    }

                    break;
                case 'M':
                    month = ParseDateComponentFromString(str, ref pos, presentation, MonthNames, digitWidth);
                    if (month < 0)
                    {
                        return false;
                    }

                    break;
                case 'D':
                    day = ParseIntegerValueFromString(str, ref pos, presentation, digitWidth);
                    if (day < 0)
                    {
                        return false;
                    }

                    break;
                case 'd':
                    dayOfYear = ParseIntegerValueFromString(str, ref pos, presentation, digitWidth);
                    if (dayOfYear < 0)
                    {
                        return false;
                    }

                    break;
                case 'H':
                    hour = ParseIntegerValueFromString(str, ref pos, presentation, digitWidth);
                    if (hour < 0)
                    {
                        return false;
                    }

                    break;
                case 'h':
                    hour = ParseIntegerValueFromString(str, ref pos, presentation, digitWidth);
                    if (hour < 0)
                    {
                        return false;
                    }

                    break;
                case 'm':
                    minute = ParseIntegerValueFromString(str, ref pos, presentation, digitWidth);
                    if (minute < 0)
                    {
                        return false;
                    }

                    break;
                case 's':
                    second = ParseIntegerValueFromString(str, ref pos, presentation, digitWidth);
                    if (second < 0)
                    {
                        return false;
                    }

                    break;
                case 'f':
                    millisecond = ParseFractionalSeconds(str, ref pos, presentation);
                    if (millisecond < 0)
                    {
                        return false;
                    }

                    break;
                case 'P':
                    // AM/PM
                    string ampm = ParseAmPm(str, ref pos);
                    if (ampm.Length == 0)
                    {
                        return false;
                    }

                    if (string.Equals(ampm, "pm", StringComparison.OrdinalIgnoreCase))
                    {
                        if (hour >= 0 && hour < 12)
                        {
                            hour += 12;
                        }
                    }
                    else if (string.Equals(ampm, "am", StringComparison.OrdinalIgnoreCase))
                    {
                        if (hour == 12)
                        {
                            hour = 0;
                        }
                    }

                    break;
                case 'Z':
                    tzOffsetMinutes = ParseTimezoneOffset(str, ref pos);
                    break;
                case 'z':
                    tzOffsetMinutes = ParseTimezoneNameOffset(str, ref pos);
                    break;
                case 'F':
                    // Day of week - just consume it, we don't use it for calculation
                    SkipDayOfWeek(str, ref pos, presentation);
                    break;
                case 'C':
                case 'E':
                    // Calendar/Era - skip
                    SkipWord(str, ref pos);
                    break;
                case 'W':
                case 'w':
                case 'X':
                case 'x':
                    throw new JsonataException("D3136", SR.D3136_TheDateTimeComponentsInThePictureStringAreNotConsistent, 0);
                default:
                    throw new JsonataException("D3132", SR.Format(SR.D3132_UnknownComponentSpecifier, compChar), 0);
            }
        }

        // If no date/time components were extracted at all (e.g., picture was all literal text),
        // the parse is considered unsuccessful.
        if (year < 0 && month < 0 && day < 0 && dayOfYear < 0 &&
            hour < 0 && minute < 0 && second < 0 && millisecond < 0)
        {
            return false;
        }

        // Validate consistency
        if (dayOfYear >= 0)
        {
            if (year < 0)
            {
                throw new JsonataException("D3136", SR.D3136_TheDateTimeComponentsInThePictureStringAreNotConsistent, 0);
            }

            var jan1 = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var result = jan1.AddDays(dayOfYear - 1);
            month = result.Month;
            day = result.Day;
        }

        // Validate: if we have day and year but no month (and no dayOfYear), that's an error
        if (day >= 0 && year >= 0 && month < 0 && dayOfYear < 0)
        {
            throw new JsonataException("D3136", SR.D3136_TheDateTimeComponentsInThePictureStringAreNotConsistent, 0);
        }

        // Validate: if we have minute and second but no hour, that's an error
        if (hour < 0 && (minute >= 0 || second >= 0))
        {
            if (minute >= 0 && second >= 0)
            {
                throw new JsonataException("D3136", SR.D3136_TheDateTimeComponentsInThePictureStringAreNotConsistent, 0);
            }
        }

        // Default unspecified date parts
        if (year < 0)
        {
            year = DateTimeOffset.UtcNow.Year;
        }

        if (month < 0)
        {
            if (day >= 0 || hour >= 0)
            {
                month = DateTimeOffset.UtcNow.Month;
            }
            else
            {
                month = 1;
            }
        }

        if (day < 0)
        {
            if (hour >= 0)
            {
                day = DateTimeOffset.UtcNow.Day;
            }
            else
            {
                day = 1;
            }
        }

        if (hour < 0)
        {
            hour = 0;
        }

        if (minute < 0)
        {
            minute = 0;
        }

        if (second < 0)
        {
            second = 0;
        }

        if (millisecond < 0)
        {
            millisecond = 0;
        }

        try
        {
            var offset = tzOffsetMinutes.HasValue ? TimeSpan.FromMinutes(tzOffsetMinutes.Value) : TimeSpan.Zero;
            var dto = new DateTimeOffset(year, month, day, hour, minute, second, millisecond, offset);
            millis = dto.ToUnixTimeMilliseconds();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Formats an integer using an XPath integer picture string.
    /// </summary>
    /// <param name="value">The integer value to format.</param>
    /// <param name="picture">The XPath integer picture string.</param>
    /// <returns>The formatted string.</returns>
    public static string FormatInteger(long value, string picture)
    {
        Utf8ValueStringBuilder sb = new(stackalloc byte[64]);
        FormatInteger(value, picture, ref sb);
#if NET
        string result = Encoding.UTF8.GetString(sb.AsSpan());
#else
        string result = Encoding.UTF8.GetString(sb.AsSpan().ToArray());
#endif
        sb.Dispose();
        return result;
    }

    /// <summary>
    /// Formats an integer using the XPath picture string,
    /// writing the UTF-8 result directly to a caller-supplied buffer.
    /// </summary>
    /// <param name="value">The integer value to format.</param>
    /// <param name="picture">The XPath integer picture string.</param>
    /// <param name="destination">The destination buffer for UTF-8 output.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the destination was large enough; otherwise <see langword="false"/>.</returns>
    internal static bool TryFormatInteger(long value, string picture, Span<byte> destination, out int bytesWritten)
    {
        Utf8ValueStringBuilder sb = new(destination);
        FormatInteger(value, picture, ref sb);
        bool success = sb.TryCopyTo(destination, out bytesWritten);
        sb.Dispose();
        return success;
    }

    internal static void FormatInteger(long value, string picture, ref Utf8ValueStringBuilder sb)
    {
        // Split picture on ';' for ordinal modifier
        string primary;
        bool isOrdinal = false;
        int semiIdx = picture.IndexOf(';');
        if (semiIdx >= 0)
        {
            primary = picture.Substring(0, semiIdx);
            string modifier = picture.Substring(semiIdx + 1);
            if (modifier.IndexOf('o') >= 0)
            {
                isOrdinal = true;
            }
        }
        else
        {
            primary = picture;
        }

        if (primary.Length == 0)
        {
            primary = "0";
        }

        FormatIntegerWithPresentation(value, primary, isOrdinal, ref sb);
    }

    /// <summary>
    /// Formats a large integer (as a double) using an XPath integer picture string.
    /// Used when the value is outside the range of <see cref="long"/>.
    /// </summary>
    /// <param name="value">The integer value as a double.</param>
    /// <param name="picture">The XPath integer picture string.</param>
    /// <returns>The formatted string.</returns>
    public static string FormatInteger(double value, string picture)
    {
        if (value >= long.MinValue && value <= long.MaxValue)
        {
            return FormatInteger((long)value, picture);
        }

        string primary;
        bool isOrdinal = false;
        int semiIdx = picture.IndexOf(';');
        if (semiIdx >= 0)
        {
            primary = picture.Substring(0, semiIdx);
            string modifier = picture.Substring(semiIdx + 1);
            if (modifier.IndexOf('o') >= 0)
            {
                isOrdinal = true;
            }
        }
        else
        {
            primary = picture;
        }

        if (primary.Length == 0)
        {
            primary = "0";
        }

        if (primary == "W" || primary == "w" || primary == "Ww")
        {
            bool isNegative = value < 0;
            double absValue = Math.Abs(value);
            string words = NumberToWordsLarge(absValue);
            if (isNegative)
            {
                words = "minus " + words;
            }

            if (isOrdinal)
            {
                words = MakeOrdinalWords(words);
            }

            return ApplyWordCasing(words, primary);
        }

        // For non-word patterns, format using scientific notation
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an integer from a string using an XPath integer picture string.
    /// </summary>
    /// <param name="str">The string to parse.</param>
    /// <param name="picture">The XPath integer picture string.</param>
    /// <param name="value">The parsed integer value.</param>
    /// <returns><see langword="true"/> if the string was successfully parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParseInteger(string str, string picture, out long value)
    {
        value = 0;

        string primary;
        bool isOrdinal = false;
        int semiIdx = picture.IndexOf(';');
        if (semiIdx >= 0)
        {
            primary = picture.Substring(0, semiIdx);
            string modifier = picture.Substring(semiIdx + 1);
            if (modifier.IndexOf('o') >= 0)
            {
                isOrdinal = true;
            }
        }
        else
        {
            primary = picture;
        }

        if (primary.Length == 0)
        {
            primary = "0";
        }

        return TryParseIntegerWithPresentation(str, primary, isOrdinal, out value);
    }

    /// <summary>
    /// Parses an integer from a string using an XPath integer picture string, returning a double
    /// to handle values outside the range of <see cref="long"/>.
    /// </summary>
    /// <param name="str">The string to parse.</param>
    /// <param name="picture">The XPath integer picture string.</param>
    /// <param name="value">The parsed value as a double.</param>
    /// <returns><see langword="true"/> if the string was successfully parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParseInteger(string str, string picture, out double value)
    {
        value = 0;

        string primary;
        bool isOrdinal = false;
        int semiIdx = picture.IndexOf(';');
        if (semiIdx >= 0)
        {
            primary = picture.Substring(0, semiIdx);
            string modifier = picture.Substring(semiIdx + 1);
            if (modifier.IndexOf('o') >= 0)
            {
                isOrdinal = true;
            }
        }
        else
        {
            primary = picture;
        }

        if (primary.Length == 0)
        {
            primary = "0";
        }

        if (primary == "W" || primary == "w" || primary == "Ww")
        {
            return TryParseWordsToNumberDouble(str, isOrdinal, out value);
        }

        // For other patterns, use the long version
        if (TryParseIntegerWithPresentation(str, primary, isOrdinal, out long longValue))
        {
            value = longValue;
            return true;
        }

        return false;
    }

    internal static void FormatIntegerWithPresentation(long value, string presentation, bool isOrdinal, ref Utf8ValueStringBuilder result)
    {
        // Detect the format type from the first meaningful character
        if (presentation == "I")
        {
            ToRomanNumerals(value, true, ref result);
            return;
        }

        if (presentation == "i")
        {
            ToRomanNumerals(value, false, ref result);
            return;
        }

        if (presentation == "W" || presentation == "w" || presentation == "Ww")
        {
            string words = FormatAsWords(value, presentation, isOrdinal);
            AppendAsciiString(ref result, words);
            return;
        }

        if (presentation == "A")
        {
            ToAlpha(value, true, ref result);
            return;
        }

        if (presentation == "a")
        {
            ToAlpha(value, false, ref result);
            return;
        }

        if (presentation == "N" || presentation == "n" || presentation == "Nn")
        {
            throw new JsonataException("D3133", SR.D3133_TheComponentCannotBeRepresentedAsAName, 0);
        }

        // Check if it's a decimal digit pattern
        if (IsDecimalDigitPattern(presentation))
        {
            FormatDecimalDigit(value, presentation, isOrdinal, ref result);
            return;
        }

        // Check for single '#' which is an error in formatInteger context
        if (presentation == "#")
        {
            throw new JsonataException("D3130", SR.D3130_TheFormatPictureStringIsNotValid, 0);
        }

        throw new JsonataException("D3130", SR.D3130_TheFormatPictureStringIsNotValid, 0);
    }

    private static bool TryParseIntegerWithPresentation(string str, string presentation, bool isOrdinal, out long value)
    {
        value = 0;

        if (presentation == "I" || presentation == "i")
        {
            value = FromRomanNumerals(str);
            return true;
        }

        if (presentation == "W" || presentation == "w" || presentation == "Ww")
        {
            return TryParseWordsToNumber(str, isOrdinal, out value);
        }

        if (presentation == "A")
        {
            value = FromAlpha(str, true);
            return true;
        }

        if (presentation == "a")
        {
            value = FromAlpha(str, false);
            return true;
        }

        if (presentation == "#")
        {
            throw new JsonataException("D3130", SR.D3130_TheFormatPictureStringIsNotValid, 0);
        }

        if (IsDecimalDigitPattern(presentation))
        {
            return TryParseDecimalDigit(str, presentation, isOrdinal, out value);
        }

        throw new JsonataException("D3130", SR.D3130_TheFormatPictureStringIsNotValid, 0);
    }

    private static void FormatComponent(DateTimeOffset dt, string marker, ref Utf8ValueStringBuilder sb)
    {
        if (marker.Length == 0)
        {
            return;
        }

        char comp = marker[0];
        string rest = marker.Length > 1 ? marker.Substring(1) : string.Empty;

        // Parse optional width modifier (after comma).
        // The presentation modifier can itself contain commas (e.g. '#,##0' as a grouping pattern),
        // so we scan from right to left for the comma that starts a valid width modifier.
        int maxWidth = -1;
        int minWidth = -1;
        int widthCommaIdx = -1;
        for (int ci = rest.Length - 1; ci >= 0; ci--)
        {
            if (rest[ci] == ',')
            {
                string candidate = rest.Substring(ci + 1);
                if (IsValidWidthModifier(candidate))
                {
                    widthCommaIdx = ci;
                    break;
                }
            }
        }

        bool hardMaxWidth = false;
        if (widthCommaIdx >= 0)
        {
            string widthSpec = rest.Substring(widthCommaIdx + 1);
            rest = rest.Substring(0, widthCommaIdx);
            bool isRange = widthSpec.IndexOf('-') >= 0;
            ParseWidthModifier(widthSpec, out minWidth, out maxWidth);
            hardMaxWidth = isRange;

            // When the width modifier is a single value (not a range), the maxWidth
            // should not truncate below the mandatory digit count in the presentation.
            // When it's an explicit range, the max is authoritative.
            if (!isRange && maxWidth >= 0)
            {
                int mandatoryFromPres = CountMandatoryDigits(rest);
                if (mandatoryFromPres > maxWidth)
                {
                    maxWidth = mandatoryFromPres;
                }
            }
        }

        string presentation = rest;

        // Apply XPath 3.1 default presentation modifiers per component
        if (presentation.Length == 0)
        {
            presentation = comp switch
            {
                'Y' => "1",
                'M' => "1",
                'D' => "1",
                'd' => "1",
                'F' => "n",
                'H' => "1",
                'h' => "1",
                'P' => "n",
                'm' => "01",
                's' => "01",
                'f' => "1",
                'Z' => "01:01",
                'W' => "1",
                'w' => "1",
                'x' => "1",
                'X' => "1",
                _ => "1",
            };
        }

        switch (comp)
        {
            case 'Y':
                FormatDateValue(dt.Year, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case 'M':
                FormatDateValueOrName(dt.Month, presentation, minWidth, maxWidth, hardMaxWidth, MonthNames, ref sb);
                break;
            case 'D':
                FormatDateValue(dt.Day, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case 'd':
                FormatDateValue(dt.DayOfYear, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case 'F':
                int isoDay = GetIsoDayOfWeek(dt.DayOfWeek);
                FormatDateValueOrName(isoDay, presentation, minWidth, maxWidth, hardMaxWidth, DayNames, ref sb);
                break;
            case 'H':
                FormatDateValue(dt.Hour, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case 'h':
                int h12 = dt.Hour % 12;
                if (h12 == 0)
                {
                    h12 = 12;
                }

                FormatDateValue(h12, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case 'P':
                if (presentation.Length == 0 || presentation == "n")
                {
                    sb.Append(dt.Hour < 12 ? "am"u8 : "pm"u8);
                }
                else if (presentation == "N")
                {
                    sb.Append(dt.Hour < 12 ? "AM"u8 : "PM"u8);
                }
                else
                {
                    sb.Append(dt.Hour < 12 ? "am"u8 : "pm"u8);
                }

                break;
            case 'm':
                FormatDateValue(dt.Minute, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case 's':
                FormatDateValue(dt.Second, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case 'f':
                FormatFractionalSeconds(dt.Millisecond, presentation, ref sb);
                break;
            case 'Z':
                FormatTimezoneOffset(dt.Offset, presentation, ref sb);
                break;
            case 'z':
                FormatTimezoneGmt(dt.Offset, ref sb);
                break;
            case 'W':
                FormatDateValue(GetIsoWeekOfYear(dt), presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case 'w':
                FormatDateValue(GetWeekOfMonth(dt), presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case 'x':
                FormatDateValueOrName(GetMonthOfWeek(dt), presentation, minWidth, maxWidth, hardMaxWidth, MonthNames, ref sb);
                break;
            case 'X':
                FormatDateValue(GetIsoWeekYear(dt), presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case 'C':
                sb.Append("ISO"u8);
                break;
            case 'E':
                sb.Append("ISO"u8);
                break;
            default:
                throw new JsonataException("D3132", SR.Format(SR.D3132_UnknownComponentSpecifier, comp), 0);
        }
    }

    private static void FormatDateValue(int value, string presentation, int minWidth, int maxWidth, bool hardMaxWidth, ref Utf8ValueStringBuilder sb)
    {
        bool isOrdinal = false;
        string pres = presentation;

        // Check for trailing 'o' ordinal modifier in presentation
        if (pres.Length > 0 && pres[pres.Length - 1] == 'o')
        {
            isOrdinal = true;
            pres = pres.Substring(0, pres.Length - 1);
        }

        if (pres.Length == 0)
        {
            pres = "1";
        }

        Utf8ValueStringBuilder temp = new(stackalloc byte[64]);
        FormatIntegerWithPresentation(value, pres, isOrdinal, ref temp);
        ReadOnlySpan<byte> formatted = temp.AsSpan();

        if (maxWidth >= 0 && formatted.Length > maxWidth)
        {
            if (hardMaxWidth)
            {
                formatted = formatted.Slice(formatted.Length - maxWidth);
            }
            else
            {
                int mandatoryFromPres = CountMandatoryDigits(pres);
                int effectiveMax = Math.Max(maxWidth, mandatoryFromPres);
                if (formatted.Length > effectiveMax)
                {
                    formatted = formatted.Slice(formatted.Length - effectiveMax);
                }
            }
        }

        if (minWidth >= 0 && formatted.Length < minWidth)
        {
            sb.Append((byte)'0', minWidth - formatted.Length);
        }

        sb.Append(formatted);
        temp.Dispose();
    }

    private static void FormatDateValueOrName(int value, string presentation, int minWidth, int maxWidth, bool hardMaxWidth, string[] names, ref Utf8ValueStringBuilder sb)
    {
        if (presentation.Length == 0)
        {
            // Default numeric
            FormatDateValue(value, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
            return;
        }

        // Check for name presentations
        bool isName = false;
        bool isUpper = false;
        bool isLower = false;

        if (presentation == "N")
        {
            isName = true;
            isUpper = true;
        }
        else if (presentation == "n")
        {
            isName = true;
            isLower = true;
        }
        else if (presentation == "Nn")
        {
            isName = true;
        }

        if (isName && names.Length > 0)
        {
            int idx = value - 1;
            if (idx < 0 || idx >= names.Length)
            {
                sb.Append(value);
                return;
            }

            string name = names[idx];
            int nameLen = maxWidth >= 0 && name.Length > maxWidth ? maxWidth : name.Length;

            if (isUpper)
            {
                Span<byte> dest = sb.AppendSpan(nameLen);
                for (int j = 0; j < nameLen; j++)
                {
                    dest[j] = (byte)char.ToUpperInvariant(name[j]);
                }
            }
            else if (isLower)
            {
                Span<byte> dest = sb.AppendSpan(nameLen);
                for (int j = 0; j < nameLen; j++)
                {
                    dest[j] = (byte)char.ToLowerInvariant(name[j]);
                }
            }
            else
            {
                // Title case — already title case in the names array
                Span<byte> dest = sb.AppendSpan(nameLen);
                for (int j = 0; j < nameLen; j++)
                {
                    dest[j] = (byte)name[j];
                }
            }

            return;
        }

        FormatDateValue(value, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
    }

    private static void FormatFractionalSeconds(int milliseconds, string presentation, ref Utf8ValueStringBuilder sb)
    {
        // Count digit characters in presentation for number of fractional digits
        int digits = 0;
        foreach (char c in presentation)
        {
            if (c >= '0' && c <= '9')
            {
                digits++;
            }
        }

        if (digits == 0)
        {
            digits = 1;
        }

        // Format milliseconds as 3-digit zero-padded
        Span<byte> msBuf = stackalloc byte[3];
        msBuf[0] = (byte)('0' + (milliseconds / 100));
        msBuf[1] = (byte)('0' + ((milliseconds / 10) % 10));
        msBuf[2] = (byte)('0' + (milliseconds % 10));

        if (digits <= 3)
        {
            sb.Append(msBuf.Slice(0, digits));
        }
        else
        {
            sb.Append(msBuf);
            sb.Append((byte)'0', digits - 3);
        }
    }

    private static void FormatTimezoneOffset(TimeSpan offset, string presentation, ref Utf8ValueStringBuilder sb)
    {
        bool useColon = true;
        bool useZForUtc = false;
        int digits;

        // Parse timezone presentation
        // "01:01" = +HH:MM zero-padded with colon
        // "0101" = +HHMM zero-padded no colon
        // "01:01t" = Z for UTC, else +HH:MM
        // "0101t" = Z for UTC, else +HHMM
        // "" or default = +HH:MM
        // "0" = minimal (no leading zero on hour, include minutes only if non-zero)
        string p = presentation;
        if (p.Length > 0 && p[p.Length - 1] == 't')
        {
            useZForUtc = true;
            p = p.Substring(0, p.Length - 1);
        }

        if (useZForUtc && offset == TimeSpan.Zero)
        {
            sb.Append((byte)'Z');
            return;
        }

        // Detect pattern
        if (p.Contains(":"))
        {
            // Colon-separated
            useColon = true;
            digits = 2; // zero-padded
        }
        else if (p == "0")
        {
            // Minimal: no leading zero on hour, minutes only if non-zero
            useColon = false;
            digits = 0;
        }
        else if (p.Length == 4)
        {
            // "0101" or "0001" etc - 4 digit no colon
            useColon = false;
            digits = 2;
        }
        else if (p.Length >= 6)
        {
            // 6+ digits is an error
            throw new JsonataException("D3134", SR.D3134_TheTimezoneComponentOfThePictureStringIsNotValid, 0);
        }
        else if (p.Length == 0)
        {
            // Default: +HH:MM with colon
            useColon = true;
            digits = 2;
        }
        else
        {
            useColon = false;
            digits = 2;
        }

        sb.Append(offset >= TimeSpan.Zero ? (byte)'+' : (byte)'-');

        int totalMinutes = (int)Math.Abs(offset.TotalMinutes);
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;

        if (digits == 0)
        {
            // Minimal format
            sb.Append(hours);
            if (minutes > 0)
            {
                sb.Append((byte)':');
                if (minutes < 10)
                {
                    sb.Append((byte)'0');
                }

                sb.Append(minutes);
            }
        }
        else
        {
            if (hours < 10)
            {
                sb.Append((byte)'0');
            }

            sb.Append(hours);
            if (useColon)
            {
                sb.Append((byte)':');
            }

            if (minutes < 10)
            {
                sb.Append((byte)'0');
            }

            sb.Append(minutes);
        }
    }

    private static void FormatTimezoneGmt(TimeSpan offset, ref Utf8ValueStringBuilder sb)
    {
        sb.Append("GMT"u8);
        if (offset != TimeSpan.Zero)
        {
            sb.Append(offset >= TimeSpan.Zero ? (byte)'+' : (byte)'-');
            int totalMinutes = (int)Math.Abs(offset.TotalMinutes);
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            if (hours < 10)
            {
                sb.Append((byte)'0');
            }

            sb.Append(hours);
            sb.Append((byte)':');

            if (minutes < 10)
            {
                sb.Append((byte)'0');
            }

            sb.Append(minutes);
        }
    }

    private static int GetIsoDayOfWeek(DayOfWeek dow)
    {
        return dow == DayOfWeek.Sunday ? 7 : (int)dow;
    }

    private static int GetIsoWeekOfYear(DateTimeOffset dt)
    {
        // ISO 8601 week calculation
        // Week 1 is the week containing the first Thursday of the year
        DateTime d = dt.UtcDateTime.Date;
        DayOfWeek dow = d.DayOfWeek;

        // ISO: Monday=1 ... Sunday=7
        int isoDow = dow == DayOfWeek.Sunday ? 7 : (int)dow;

        // Thursday of this week
        DateTime thursday = d.AddDays(4 - isoDow);
        int year = thursday.Year;

        // Jan 1 of that year
        DateTime jan1 = new DateTime(year, 1, 1);
        int jan1Dow = jan1.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)jan1.DayOfWeek;

        // Monday of week 1
        DateTime week1Monday = jan1.AddDays(1 - jan1Dow);
        if (jan1Dow > 4)
        {
            week1Monday = week1Monday.AddDays(7);
        }

        // Monday of current week
        DateTime currentMonday = d.AddDays(1 - isoDow);
        int weekNumber = ((currentMonday - week1Monday).Days / 7) + 1;

        return weekNumber;
    }

    private static int GetIsoWeekYear(DateTimeOffset dt)
    {
        DateTime d = dt.UtcDateTime.Date;
        DayOfWeek dow = d.DayOfWeek;
        int isoDow = dow == DayOfWeek.Sunday ? 7 : (int)dow;

        // Thursday of this week determines the year
        DateTime thursday = d.AddDays(4 - isoDow);
        return thursday.Year;
    }

    private static int GetWeekOfMonth(DateTimeOffset dt)
    {
        // ISO week-of-month: weeks start Monday, the reference month is the month
        // containing the Thursday of the week (same as GetMonthOfWeek)
        DateTime d = dt.UtcDateTime.Date;
        int isoDow = d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

        // Thursday of the current week determines which month this week belongs to
        DateTime thursday = d.AddDays(4 - isoDow);
        int refMonth = thursday.Month;
        int refYear = thursday.Year;

        // First day of the reference month
        DateTime firstOfMonth = new DateTime(refYear, refMonth, 1);
        int firstDow = firstOfMonth.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)firstOfMonth.DayOfWeek;

        // Monday of the first ISO week of the reference month
        // (week 1 contains the first Thursday of the month)
        DateTime firstMonday = firstOfMonth.AddDays(firstDow <= 4 ? (1 - firstDow) : (8 - firstDow));

        // Monday of the current week
        DateTime currentMonday = d.AddDays(1 - isoDow);

        int weekOfMonth = ((currentMonday - firstMonday).Days / 7) + 1;
        return weekOfMonth;
    }

    private static int GetMonthOfWeek(DateTimeOffset dt)
    {
        // The month that the Thursday of the current ISO week falls in
        DateTime d = dt.UtcDateTime.Date;
        int isoDow = d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;
        DateTime thursday = d.AddDays(4 - isoDow);
        return thursday.Month;
    }

    /// <summary>
    /// Checks if a string is a valid width modifier: a number, '*', or 'n-m' range.
    /// </summary>
    private static bool IsValidWidthModifier(string spec)
    {
        if (spec.Length == 0)
        {
            return false;
        }

        // Split on '-' for range
        int dash = spec.IndexOf('-');
        if (dash >= 0)
        {
            string left = spec.Substring(0, dash).Trim();
            string right = spec.Substring(dash + 1).Trim();
            return IsWidthPart(left) && IsWidthPart(right);
        }

        return IsWidthPart(spec.Trim());
    }

    private static bool IsWidthPart(string part)
    {
        if (part == "*")
        {
            return true;
        }

        if (part.Length == 0)
        {
            return false;
        }

        foreach (char c in part)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static int CountMandatoryDigits(string presentation)
    {
        // Count digit positions (0-9) and optional-digit markers (#) — these
        // define how many digit characters the presentation pattern requires.
        int count = 0;
        foreach (char c in presentation)
        {
            if ((c >= '0' && c <= '9') || c == '#')
            {
                count++;
            }
            else
            {
                int ug = GetUnicodeDecimalGroup(c);
                if (ug >= 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static void ParseWidthModifier(string spec, out int minWidth, out int maxWidth)
    {
        minWidth = -1;
        maxWidth = -1;

        int dash = spec.IndexOf('-');
        if (dash >= 0)
        {
            string minStr = spec.Substring(0, dash).Trim();
            string maxStr = spec.Substring(dash + 1).Trim();

            if (minStr == "*")
            {
                minWidth = -1;
            }
            else if (minStr.Length > 0 && int.TryParse(minStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mn))
            {
                minWidth = mn;
            }

            if (maxStr == "*")
            {
                maxWidth = -1;
            }
            else if (maxStr.Length > 0 && int.TryParse(maxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mx))
            {
                maxWidth = mx;
            }
        }
        else
        {
            // Single value = both minimum and maximum width
            if (spec == "*")
            {
                minWidth = -1;
            }
            else if (int.TryParse(spec.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int w))
            {
                minWidth = w;
                maxWidth = w;
            }
        }
    }

    private static bool IsDecimalDigitPattern(string presentation)
    {
        if (presentation.Length == 0)
        {
            return false;
        }

        // A decimal digit pattern contains at least one digit character (0-9 or Unicode digit)
        // and optionally '#' and grouping separators
        bool hasDigit = false;
        bool hasPosition = false; // true if we've seen '#' or a digit
        int? detectedDecimalGroup = null;

        foreach (char c in presentation)
        {
            if (c == '#')
            {
                hasPosition = true;
                continue;
            }

            if (c >= '0' && c <= '9')
            {
                if (detectedDecimalGroup.HasValue && detectedDecimalGroup.Value != 0)
                {
                    throw new JsonataException("D3131", SR.D3131_TheFormatPictureStringContainsMixedDecimalDigitGroups, 0);
                }

                detectedDecimalGroup = 0;
                hasDigit = true;
                hasPosition = true;
                continue;
            }

            // Check unicode decimal digit
            int unicodeGroup = GetUnicodeDecimalGroup(c);
            if (unicodeGroup >= 0)
            {
                if (detectedDecimalGroup.HasValue && detectedDecimalGroup.Value != unicodeGroup)
                {
                    throw new JsonataException("D3131", SR.D3131_TheFormatPictureStringContainsMixedDecimalDigitGroups, 0);
                }

                detectedDecimalGroup = unicodeGroup;
                hasDigit = true;
                hasPosition = true;
                continue;
            }

            // Must be a grouping separator if there's already been a digit or #
            if (!hasPosition)
            {
                return false;
            }
        }

        return hasDigit;
    }

    private static int GetUnicodeDecimalGroup(char c)
    {
        // Arabic-Indic digits ٠-٩ (U+0660-U+0669)
        if (c >= '\u0660' && c <= '\u0669')
        {
            return 0x0660;
        }

        // Fullwidth digits ０-９ (U+FF10-U+FF19)
        if (c >= '\uFF10' && c <= '\uFF19')
        {
            return 0xFF10;
        }

        // Thai digits ๐-๙ (U+0E50-U+0E59)
        if (c >= '\u0E50' && c <= '\u0E59')
        {
            return 0x0E50;
        }

        // Devanagari digits ०-९ (U+0966-U+096F)
        if (c >= '\u0966' && c <= '\u096F')
        {
            return 0x0966;
        }

        return -1;
    }

    private static void FormatDecimalDigit(long value, string presentation, bool isOrdinal, ref Utf8ValueStringBuilder result)
    {
        bool isNegative = value < 0;
        long absValue = Math.Abs(value);

        // Detect the digit base (ASCII or Unicode)
        int unicodeBase = 0;
        foreach (char c in presentation)
        {
            if (c >= '0' && c <= '9')
            {
                unicodeBase = 0;
                break;
            }

            int ug = GetUnicodeDecimalGroup(c);
            if (ug >= 0)
            {
                unicodeBase = ug;
                break;
            }
        }

        // Count mandatory digits (0s in pattern), find grouping separators
        int mandatoryDigits = 0;

        // Max 20 group separators in a pattern is more than enough
        Span<(int Position, char Separator)> groupSeparators = stackalloc (int, char)[20];
        int groupSepCount = 0;
        int digitCount = 0;

        // Process pattern from right to left for grouping
        for (int i = presentation.Length - 1; i >= 0; i--)
        {
            char c = presentation[i];
            if (c == '0' || c == '#' || (c >= '1' && c <= '9'))
            {
                digitCount++;
                if (c == '0' || (c >= '1' && c <= '9'))
                {
                    mandatoryDigits = digitCount;
                }
            }
            else
            {
                int ug = GetUnicodeDecimalGroup(c);
                if (ug >= 0)
                {
                    digitCount++;
                    mandatoryDigits = digitCount;
                }
                else if (groupSepCount < groupSeparators.Length)
                {
                    // Grouping separator
                    groupSeparators[groupSepCount++] = (digitCount, c);
                }
            }
        }

        if (mandatoryDigits == 0)
        {
            mandatoryDigits = 1;
        }

        // Format digits into a byte buffer (max 20 digits for a long)
        Span<byte> digitBuf = stackalloc byte[20];
        int digitLen;
        if (!Utf8Formatter.TryFormat(absValue, digitBuf, out digitLen))
        {
            // Should never happen for a long
            digitLen = 0;
        }

        // Pad left with '0' if needed
        if (digitLen < mandatoryDigits)
        {
            int pad = mandatoryDigits - digitLen;
            digitBuf.Slice(0, digitLen).CopyTo(digitBuf.Slice(pad));
            digitBuf.Slice(0, pad).Fill((byte)'0');
            digitLen = mandatoryDigits;
        }

        ReadOnlySpan<byte> digits = digitBuf.Slice(0, digitLen);

        if (isNegative)
        {
            result.Append((byte)'-');
        }

        if (unicodeBase != 0)
        {
            // Unicode digits: convert each ASCII digit to the Unicode digit and encode as UTF-8
            if (groupSepCount > 0)
            {
                Utf8ValueStringBuilder temp = new(stackalloc byte[64]);
                InsertGroupingSeparators(digits, groupSeparators.Slice(0, groupSepCount), ref temp);
                ReadOnlySpan<byte> grouped = temp.AsSpan();
                for (int i = 0; i < grouped.Length; i++)
                {
                    byte b = grouped[i];
                    if (b >= (byte)'0' && b <= (byte)'9')
                    {
                        AppendChar(ref result, (char)(unicodeBase + (b - '0')));
                    }
                    else
                    {
                        result.Append(b);
                    }
                }

                temp.Dispose();
            }
            else
            {
                for (int i = 0; i < digits.Length; i++)
                {
                    AppendChar(ref result, (char)(unicodeBase + (digits[i] - '0')));
                }
            }
        }
        else if (groupSepCount > 0)
        {
            InsertGroupingSeparators(digits, groupSeparators.Slice(0, groupSepCount), ref result);
        }
        else
        {
            result.Append(digits);
        }

        if (isOrdinal)
        {
            AppendOrdinalSuffix(ref result, absValue);
        }
    }

    private static void InsertGroupingSeparators(
        scoped ReadOnlySpan<byte> digits,
        scoped ReadOnlySpan<(int Position, char Separator)> separators,
        ref Utf8ValueStringBuilder result)
    {
        if (separators.Length == 0)
        {
            result.Append(digits);
            return;
        }

        // Check if all separators use the same character and are regularly spaced
        bool isRegular = true;
        char sepChar = separators[0].Separator;
        int groupSize = separators[0].Position;

        for (int i = 1; i < separators.Length; i++)
        {
            if (separators[i].Separator != sepChar)
            {
                isRegular = false;
                break;
            }

            if (separators[i].Position != groupSize * (i + 1))
            {
                isRegular = false;
                break;
            }
        }

        if (isRegular)
        {
            // Regular grouping - build left-to-right with separator insertion
            for (int i = 0; i < digits.Length; i++)
            {
                int posFromRight = digits.Length - i;
                if (posFromRight % groupSize == 0 && i > 0)
                {
                    AppendChar(ref result, sepChar);
                }

                result.Append(digits[i]);
            }
        }
        else
        {
            // Non-regular grouping: sort separators by position descending, build left-to-right
            Span<(int Position, char Separator)> sorted = stackalloc (int, char)[separators.Length];
            separators.CopyTo(sorted);

            // Sort descending by position so we encounter them left-to-right (insertion sort)
            for (int si = 1; si < sorted.Length; si++)
            {
                var key = sorted[si];
                int sj = si - 1;
                while (sj >= 0 && sorted[sj].Position < key.Position)
                {
                    sorted[sj + 1] = sorted[sj];
                    sj--;
                }

                sorted[sj + 1] = key;
            }

            int sepIdx = 0;
            for (int i = 0; i < digits.Length; i++)
            {
                result.Append(digits[i]);
                int posFromRight = digits.Length - i - 1;
                if (sepIdx < sorted.Length && posFromRight == sorted[sepIdx].Position && i < digits.Length - 1)
                {
                    AppendChar(ref result, sorted[sepIdx].Separator);
                    sepIdx++;
                }
            }
        }
    }

    private static void AppendOrdinalSuffix(ref Utf8ValueStringBuilder sb, long value)
    {
        long lastTwo = value % 100;
        long lastOne = value % 10;

        if (lastTwo >= 11 && lastTwo <= 13)
        {
            sb.Append("th"u8);
            return;
        }

        switch (lastOne)
        {
            case 1:
                sb.Append("st"u8);
                break;
            case 2:
                sb.Append("nd"u8);
                break;
            case 3:
                sb.Append("rd"u8);
                break;
            default:
                sb.Append("th"u8);
                break;
        }
    }

    private static readonly (string Symbol, int Value)[] RomanValues =
    {
        ("M", 1000), ("CM", 900), ("D", 500), ("CD", 400),
        ("C", 100), ("XC", 90), ("L", 50), ("XL", 40),
        ("X", 10), ("IX", 9), ("V", 5), ("IV", 4), ("I", 1),
    };

    private static void ToRomanNumerals(long value, bool uppercase, ref Utf8ValueStringBuilder sb)
    {
        if (value <= 0)
        {
            return;
        }

        long remaining = value;
        byte offset = uppercase ? (byte)0 : (byte)('a' - 'A');

        // M=1000, CM=900, D=500, CD=400, C=100, XC=90, L=50, XL=40, X=10, IX=9, V=5, IV=4, I=1
        AppendRomanGroup(ref sb, ref remaining, 1000, (byte)('M' + offset));
        AppendRomanPair(ref sb, ref remaining, 900, (byte)('C' + offset), (byte)('M' + offset));
        AppendRomanGroup(ref sb, ref remaining, 500, (byte)('D' + offset));
        AppendRomanPair(ref sb, ref remaining, 400, (byte)('C' + offset), (byte)('D' + offset));
        AppendRomanGroup(ref sb, ref remaining, 100, (byte)('C' + offset));
        AppendRomanPair(ref sb, ref remaining, 90, (byte)('X' + offset), (byte)('C' + offset));
        AppendRomanGroup(ref sb, ref remaining, 50, (byte)('L' + offset));
        AppendRomanPair(ref sb, ref remaining, 40, (byte)('X' + offset), (byte)('L' + offset));
        AppendRomanGroup(ref sb, ref remaining, 10, (byte)('X' + offset));
        AppendRomanPair(ref sb, ref remaining, 9, (byte)('I' + offset), (byte)('X' + offset));
        AppendRomanGroup(ref sb, ref remaining, 5, (byte)('V' + offset));
        AppendRomanPair(ref sb, ref remaining, 4, (byte)('I' + offset), (byte)('V' + offset));
        AppendRomanGroup(ref sb, ref remaining, 1, (byte)('I' + offset));
    }

    private static void AppendRomanGroup(ref Utf8ValueStringBuilder sb, ref long remaining, int value, byte symbol)
    {
        while (remaining >= value)
        {
            sb.Append(symbol);
            remaining -= value;
        }
    }

    private static void AppendRomanPair(ref Utf8ValueStringBuilder sb, ref long remaining, int value, byte first, byte second)
    {
        if (remaining >= value)
        {
            sb.Append(first);
            sb.Append(second);
            remaining -= value;
        }
    }

    private static long FromRomanNumerals(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return 0;
        }

        string upper = str.ToUpperInvariant();
        long result = 0;
        int i = 0;

        while (i < upper.Length)
        {
            if (i + 1 < upper.Length)
            {
                string two = upper.Substring(i, 2);
                bool found = false;
                foreach (var (symbol, val) in RomanValues)
                {
                    if (symbol == two)
                    {
                        result += val;
                        i += 2;
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    continue;
                }
            }

            string one = upper.Substring(i, 1);
            bool foundOne = false;
            foreach (var (symbol, val) in RomanValues)
            {
                if (symbol == one)
                {
                    result += val;
                    i++;
                    foundOne = true;
                    break;
                }
            }

            if (!foundOne)
            {
                i++;
            }
        }

        return result;
    }

    private static void ToAlpha(long value, bool uppercase, ref Utf8ValueStringBuilder sb)
    {
        if (value <= 0)
        {
            return;
        }

        // Build right-to-left into a small stack buffer (max 13 chars for long range)
        Span<byte> buf = stackalloc byte[16];
        int pos = buf.Length;
        byte baseChar = uppercase ? (byte)'A' : (byte)'a';
        long remaining = value;

        while (remaining > 0)
        {
            remaining--;
            buf[--pos] = (byte)(baseChar + (remaining % 26));
            remaining /= 26;
        }

        sb.Append(buf.Slice(pos));
    }

    private static long FromAlpha(string str, bool uppercase)
    {
        if (string.IsNullOrEmpty(str))
        {
            return 0;
        }

        char baseChar = uppercase ? 'A' : 'a';
        long result = 0;

        foreach (char c in str)
        {
            result = (result * 26) + (char.ToUpperInvariant(c) - 'A') + 1;
        }

        return result;
    }

    private static string FormatAsWords(long value, string casing, bool isOrdinal)
    {
        if (value == 0)
        {
            string zeroWord = isOrdinal ? "zeroth" : "zero";
            return ApplyWordCasing(zeroWord, casing);
        }

        bool isNegative = value < 0;
        long absValue = Math.Abs(value);

        string words = NumberToWords(absValue);

        if (isNegative)
        {
            words = "minus " + words;
        }

        if (isOrdinal)
        {
            words = MakeOrdinalWords(words);
        }

        return ApplyWordCasing(words, casing);
    }

    private static string NumberToWords(long value)
    {
        if (value == 0)
        {
            return "zero";
        }

        if (value < 0)
        {
            return "minus " + NumberToWords(-value);
        }

        // Handle very large numbers by chaining "trillion"
        if (value >= 1_000_000_000_000_000L)
        {
            // Express as X trillion(s)
            long trillions = value / 1_000_000_000_000L;
            long remainder = value % 1_000_000_000_000L;

            string trillionPart = NumberToWords(trillions) + " trillion";
            if (remainder == 0)
            {
                return trillionPart;
            }

            string sep = remainder < 100 ? " and " : ", ";
            return trillionPart + sep + NumberToWords(remainder);
        }

        var parts = new List<string>();

        foreach (var (name, scale) in ScaleWords)
        {
            if (scale > 1_000_000_000_000L)
            {
                continue; // Skip trillion, handled above
            }

            if (value >= scale)
            {
                long count = value / scale;
                value %= scale;

                if (scale >= 1000)
                {
                    parts.Add(NumberToWords(count) + " " + name);
                }
                else
                {
                    // hundred
                    parts.Add(Ones[count] + " " + name);
                }
            }
        }

        if (value > 0)
        {
            if (parts.Count > 0)
            {
                parts.Add("and");
            }

            if (value < 20)
            {
                parts.Add(Ones[value]);
            }
            else
            {
                string tens = Tens[value / 10];
                long ones = value % 10;
                if (ones > 0)
                {
                    parts.Add(tens + "-" + Ones[ones]);
                }
                else
                {
                    parts.Add(tens);
                }
            }
        }

        // Join: use ", " between major groups but " and " before last small part
        // Actually, the logic is: groups are joined by ", " except "and" tokens use " "
        var result = new StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                if (parts[i] == "and")
                {
                    result.Append(' ');
                }
                else if (i > 0 && parts[i - 1] == "and")
                {
                    result.Append(' ');
                }
                else
                {
                    result.Append(", ");
                }
            }

            result.Append(parts[i]);
        }

        return result.ToString();
    }

    private static string NumberToWordsLarge(double value)
    {
        if (value < 1_000_000_000_000.0)
        {
            return NumberToWords((long)value);
        }

        double trillions = Math.Floor(value / 1_000_000_000_000.0);
        double remainder = value - (trillions * 1_000_000_000_000.0);

        string trillionPart;
        if (trillions >= 1_000_000_000_000.0)
        {
            trillionPart = NumberToWordsLarge(trillions) + " trillion";
        }
        else
        {
            trillionPart = NumberToWords((long)trillions) + " trillion";
        }

        if (remainder < 1.0)
        {
            return trillionPart;
        }

        string sep = remainder < 100 ? " and " : ", ";
        return trillionPart + sep + NumberToWords((long)remainder);
    }

    private static string MakeOrdinalWords(string words)
    {
        // Find the last word and convert it to ordinal form
        int lastSpace = -1;
        int lastHyphen = -1;

        for (int i = words.Length - 1; i >= 0; i--)
        {
            if (words[i] == ' ' && lastSpace < 0)
            {
                lastSpace = i;
            }

            if (words[i] == '-' && lastHyphen < 0)
            {
                lastHyphen = i;
            }

            if (lastSpace >= 0 && lastHyphen >= 0)
            {
                break;
            }
        }

        int splitPos = Math.Max(lastSpace, lastHyphen);
        if (splitPos < 0)
        {
            // Single word
            return WordToOrdinal(words);
        }

        string prefix = words.Substring(0, splitPos + 1);
        string lastWord = words.Substring(splitPos + 1);

        return prefix + WordToOrdinal(lastWord);
    }

    private static string WordToOrdinal(string word)
    {
        if (OrdinalWordMap.TryGetValue(word, out string? ordinal))
        {
            return ordinal;
        }

        // Check if it ends with "y" - change to "ieth"
        if (word.EndsWith("y", StringComparison.Ordinal))
        {
            return word.Substring(0, word.Length - 1) + "ieth";
        }

        return word + "th";
    }

    private static string ApplyWordCasing(string words, string casing)
    {
        if (casing == "W")
        {
            return words.ToUpperInvariant();
        }

        if (casing == "w")
        {
            return words.ToLowerInvariant();
        }

        if (casing == "Ww")
        {
            return TitleCase(words);
        }

        return words;
    }

    private static string TitleCase(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool capitalizeNext = true;

        // Split into words and capitalize each, except "and" which stays lowercase
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == ' ' || text[i] == '-' || text[i] == ',')
            {
                sb.Append(text[i]);
                capitalizeNext = true;
                i++;
            }
            else if (capitalizeNext)
            {
                // Check if this word is "and"
                if (i + 3 < text.Length &&
                    text[i] == 'a' && text[i + 1] == 'n' && text[i + 2] == 'd' &&
                    (text[i + 3] == ' ' || text[i + 3] == '-' || text[i + 3] == ','))
                {
                    sb.Append("and");
                    i += 3;
                    capitalizeNext = false;
                }
                else
                {
                    sb.Append(char.ToUpperInvariant(text[i]));
                    capitalizeNext = false;
                    i++;
                }
            }
            else
            {
                sb.Append(text[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    private static bool TryParseWordsToNumber(string str, bool isOrdinal, out long value)
    {
        value = 0;
        if (TryParseWordsToNumberDouble(str, isOrdinal, out double dblValue))
        {
            if (dblValue >= long.MinValue && dblValue <= long.MaxValue)
            {
                value = (long)dblValue;
            }

            return true;
        }

        return false;
    }

    private static bool TryParseWordsToNumberDouble(string str, bool isOrdinal, out double value)
    {
        value = 0;
        string text = str.Trim().ToLowerInvariant();

        if (text.Length == 0)
        {
            return false;
        }

        if (text == "zero" || text == "zeroth")
        {
            value = 0;
            return true;
        }

        // If ordinal, convert ordinal words back to cardinal
        if (isOrdinal)
        {
            text = ConvertOrdinalToCardinal(text);
        }

        return TryParseCardinalWordsDouble(text, out value);
    }

    private static string ConvertOrdinalToCardinal(string text)
    {
        // Try to find and replace the last ordinal word with its cardinal form
        foreach (var kvp in OrdinalWordMap)
        {
            string ordinal = kvp.Value.ToLowerInvariant();
            if (text.EndsWith(ordinal, StringComparison.Ordinal))
            {
                string cardinal = kvp.Key.ToLowerInvariant();
                return text.Substring(0, text.Length - ordinal.Length) + cardinal;
            }
        }

        // Handle "-ieth" -> "-y"  (already handled by map for standard tens)
        // Handle generic "th" suffix
        if (text.EndsWith("th", StringComparison.Ordinal))
        {
            return text.Substring(0, text.Length - 2);
        }

        return text;
    }

    private static bool TryParseCardinalWordsDouble(string text, out double value)
    {
        value = 0;

        string cleaned = text.Replace(",", " ").Replace(" and ", " ");
        string[] tokens = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
        {
            return false;
        }

        double current = 0;
        double result = 0;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];

            int hyphen = token.IndexOf('-');
            if (hyphen > 0)
            {
                string left = token.Substring(0, hyphen);
                string right = token.Substring(hyphen + 1);
                long leftVal = WordToNumber(left);
                long rightVal = WordToNumber(right);
                if (leftVal < 0 || rightVal < 0)
                {
                    return false;
                }

                current += leftVal + rightVal;
                continue;
            }

            long scaleVal = GetScaleValue(token);
            if (scaleVal > 0)
            {
                if (scaleVal >= 1000)
                {
                    if (current == 0 && result > 0)
                    {
                        // No preceding number for this scale: multiply everything so far
                        result *= scaleVal;
                    }
                    else
                    {
                        if (current == 0)
                        {
                            current = 1;
                        }

                        if (result > 0 && result < scaleVal)
                        {
                            current += result;
                            result = 0;
                        }

                        current *= scaleVal;
                        result += current;
                        current = 0;
                    }
                }
                else
                {
                    // Hundred
                    if (current == 0)
                    {
                        current = 1;
                    }

                    current *= scaleVal;
                }

                continue;
            }

            long numVal = WordToNumber(token);
            if (numVal < 0)
            {
                return false;
            }

            current += numVal;
        }

        value = result + current;
        return true;
    }

    private static long WordToNumber(string word)
    {
        for (int i = 0; i < Ones.Length; i++)
        {
            if (string.Equals(word, Ones[i], StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        for (int i = 2; i < Tens.Length; i++)
        {
            if (string.Equals(word, Tens[i], StringComparison.OrdinalIgnoreCase))
            {
                return i * 10;
            }
        }

        return -1;
    }

    private static long GetScaleValue(string word)
    {
        foreach (var (name, val) in ScaleWords)
        {
            if (string.Equals(word, name, StringComparison.OrdinalIgnoreCase))
            {
                return val;
            }
        }

        return -1;
    }

    private static bool TryParseDecimalDigit(string str, string presentation, bool isOrdinal, out long value)
    {
        value = 0;
        string text = str;

        // Strip ordinal suffix
        if (isOrdinal)
        {
            if (text.EndsWith("st", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith("nd", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith("rd", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith("th", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(0, text.Length - 2);
            }
        }

        // Detect unicode digit base from presentation
        int unicodeBase = 0;
        foreach (char c in presentation)
        {
            int ug = GetUnicodeDecimalGroup(c);
            if (ug >= 0)
            {
                unicodeBase = ug;
                break;
            }
        }

        // Find and strip grouping separators from the presentation
        var groupChars = new HashSet<char>();
        foreach (char c in presentation)
        {
            if (c != '0' && c != '#' && !(c >= '1' && c <= '9') && GetUnicodeDecimalGroup(c) < 0)
            {
                groupChars.Add(c);
            }
        }

        // Remove grouping separator characters from text
        if (groupChars.Count > 0)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (!groupChars.Contains(c))
                {
                    sb.Append(c);
                }
            }

            text = sb.ToString();
        }

        // Convert unicode digits to ASCII
        if (unicodeBase != 0)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c >= (char)unicodeBase && c <= (char)(unicodeBase + 9))
                {
                    sb.Append((char)('0' + (c - unicodeBase)));
                }
                else
                {
                    sb.Append(c);
                }
            }

            text = sb.ToString();
        }

        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private struct PictureComponent
    {
        public bool IsLiteral;
        public string? Literal;
        public char Component;
        public string Presentation;
        public int DigitWidth;
    }

    private static List<PictureComponent> ParsePictureString(string picture)
    {
        var components = new List<PictureComponent>();
        int i = 0;

        while (i < picture.Length)
        {
            if (picture[i] == '[')
            {
                if (i + 1 < picture.Length && picture[i + 1] == '[')
                {
                    components.Add(new PictureComponent { IsLiteral = true, Literal = "[" });
                    i += 2;
                    continue;
                }

                int end = picture.IndexOf(']', i + 1);
                if (end < 0)
                {
                    throw new JsonataException("D3135", SR.D3135_PictureStringContainsAWithNoMatching, 0);
                }

                string marker = picture.Substring(i + 1, end - i - 1);
                string stripped = StripWhitespace(marker);

                if (stripped.Length == 0)
                {
                    i = end + 1;
                    continue;
                }

                char comp = stripped[0];
                string pres = stripped.Length > 1 ? stripped.Substring(1) : string.Empty;

                // Remove width modifier using last-comma approach
                int lastComma = -1;
                for (int ci = pres.Length - 1; ci >= 0; ci--)
                {
                    if (pres[ci] == ',')
                    {
                        string candidate = pres.Substring(ci + 1);
                        if (IsValidWidthModifier(candidate))
                        {
                            lastComma = ci;
                            break;
                        }
                    }
                }

                int digitWidth = 0;
                int maxWidthFromPic = -1;
                if (lastComma >= 0)
                {
                    string widthPart = pres.Substring(lastComma + 1);
                    pres = pres.Substring(0, lastComma);
                    ParseWidthModifier(widthPart, out _, out maxWidthFromPic);
                }

                // Count digit positions in presentation for fixed-width parsing
                foreach (char dc in pres)
                {
                    if ((dc >= '0' && dc <= '9') || dc == '#')
                    {
                        digitWidth++;
                    }
                }

                // If the width modifier specifies a max that's less, use it
                if (maxWidthFromPic > 0 && (digitWidth == 0 || maxWidthFromPic < digitWidth))
                {
                    digitWidth = maxWidthFromPic;
                }

                // Validate component
                if (comp == 'Y' || comp == 'M' || comp == 'D' || comp == 'd' ||
                    comp == 'H' || comp == 'h' || comp == 'm' || comp == 's' ||
                    comp == 'f' || comp == 'P' || comp == 'Z' || comp == 'z' ||
                    comp == 'F' || comp == 'C' || comp == 'E' || comp == 'W' ||
                    comp == 'w' || comp == 'X' || comp == 'x')
                {
                    // Check for name presentation which is not supported for Y
                    if (comp == 'Y' && (pres == "N" || pres == "Nn"))
                    {
                        throw new JsonataException("D3133", SR.D3133_TheYearComponentCannotBeRepresentedAsAName, 0);
                    }

                    components.Add(new PictureComponent
                    {
                        IsLiteral = false,
                        Component = comp,
                        Presentation = pres,
                        DigitWidth = digitWidth,
                    });
                }
                else
                {
                    throw new JsonataException("D3132", SR.Format(SR.D3132_UnknownComponentSpecifier, comp), 0);
                }

                i = end + 1;
            }
            else if (picture[i] == ']')
            {
                if (i + 1 < picture.Length && picture[i + 1] == ']')
                {
                    components.Add(new PictureComponent { IsLiteral = true, Literal = "]" });
                    i += 2;
                }
                else
                {
                    components.Add(new PictureComponent { IsLiteral = true, Literal = "]" });
                    i++;
                }
            }
            else
            {
                // Collect literal text
                var sb = new StringBuilder();
                while (i < picture.Length && picture[i] != '[' && picture[i] != ']')
                {
                    sb.Append(picture[i]);
                    i++;
                }

                components.Add(new PictureComponent { IsLiteral = true, Literal = sb.ToString() });
            }
        }

        return components;
    }

    private static int ParseIntegerValueFromString(string str, ref int pos, string presentation, int overrideMaxDigits = 0)
    {
        if (pos >= str.Length)
        {
            return -1;
        }

        // Check for 'N' or 'Nn' name presentation (used for year names - not supported)
        if (presentation == "N" || presentation == "Nn")
        {
            throw new JsonataException("D3133", SR.D3133_TheComponentCannotBeRepresentedAsAName, 0);
        }

        // Check for Roman numerals
        if (presentation == "I" || presentation == "i")
        {
            return ParseRomanFromString(str, ref pos);
        }

        // Check for words
        if (presentation == "w" || presentation == "W" || presentation == "Ww")
        {
            return ParseWordsFromString(str, ref pos, false);
        }

        // Check for ordinal words
        if (presentation == "wo" || presentation == "Wo" || presentation == "Wwo")
        {
            return ParseWordsFromString(str, ref pos, true);
        }

        // Check for alpha
        if (presentation == "a" || presentation == "A")
        {
            return ParseAlphaFromString(str, ref pos, presentation == "A");
        }

        // Check for ordinal suffix
        bool isOrdinal = false;
        string pres = presentation;
        if (pres.Length > 0 && pres[pres.Length - 1] == 'o')
        {
            isOrdinal = true;
            pres = pres.Substring(0, pres.Length - 1);
        }

        // Strip '#' from presentation
        pres = pres.Replace("#", string.Empty);

        // Count expected digits from presentation to limit consumption
        // when adjacent components have no separator (e.g., "201802" with [Y0001][M01])
        int maxDigits = 0;
        foreach (char dc in pres)
        {
            if (dc >= '0' && dc <= '9')
            {
                maxDigits++;
            }
        }

        // Use the override from PictureComponent.DigitWidth when available
        if (overrideMaxDigits > 1)
        {
            maxDigits = overrideMaxDigits;
        }

        // If presentation specifies more than 1 digit, use it as a fixed width
        // Otherwise (default), consume greedily
        bool fixedWidth = maxDigits > 1;

        // Parse numeric value
        int start = pos;
        bool negative = false;
        if (pos < str.Length && str[pos] == '-')
        {
            negative = true;
            pos++;
        }

        int digitsRead = 0;
        while (pos < str.Length && str[pos] >= '0' && str[pos] <= '9')
        {
            pos++;
            digitsRead++;
            if (fixedWidth && digitsRead >= maxDigits)
            {
                break;
            }
        }

        if (pos == start || (negative && pos == start + 1))
        {
            return -1;
        }

        string numStr = str.Substring(start, pos - start);
        if (!int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            return -1;
        }

        // Skip ordinal suffix if present
        if (isOrdinal && pos + 1 < str.Length)
        {
            string suffix = str.Substring(pos, 2);
            if (suffix == "st" || suffix == "nd" || suffix == "rd" || suffix == "th")
            {
                pos += 2;
            }
        }

        return result;
    }

    private static int ParseDateComponentFromString(string str, ref int pos, string presentation, string[] names, int overrideMaxDigits = 0)
    {
        if (pos >= str.Length)
        {
            return -1;
        }

        // Check for name-based parsing
        if (presentation == "N" || presentation == "n" || presentation == "Nn" ||
            (presentation.Length >= 2 && presentation[0] == 'N' && presentation[1] == 'n'))
        {
            return ParseNameFromString(str, ref pos, names);
        }

        // Check for Roman numerals
        if (presentation == "I" || presentation == "i")
        {
            return ParseRomanFromString(str, ref pos);
        }

        // Check for alpha
        if (presentation == "A" || presentation == "a")
        {
            return ParseAlphaFromString(str, ref pos, presentation == "A");
        }

        // Numeric
        return ParseIntegerValueFromString(str, ref pos, presentation, overrideMaxDigits);
    }

    private static int ParseNameFromString(string str, ref int pos, string[] names)
    {
        string remaining = str.Substring(pos);

        // Try to match from longest to shortest
        int bestMatch = -1;
        int bestLength = 0;

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];

            // Try full name
            if (remaining.Length >= name.Length &&
                string.Equals(remaining.Substring(0, name.Length), name, StringComparison.OrdinalIgnoreCase))
            {
                if (name.Length > bestLength)
                {
                    bestMatch = i + 1;
                    bestLength = name.Length;
                }
            }

            // Try abbreviations (3+ chars)
            for (int len = 3; len < name.Length; len++)
            {
                string abbrev = name.Substring(0, len);
                if (remaining.Length >= len &&
                    string.Equals(remaining.Substring(0, len), abbrev, StringComparison.OrdinalIgnoreCase))
                {
                    if (len > bestLength)
                    {
                        bestMatch = i + 1;
                        bestLength = len;
                    }
                }
            }
        }

        if (bestMatch >= 0)
        {
            pos += bestLength;
            return bestMatch;
        }

        return -1;
    }

    private static int ParseRomanFromString(string str, ref int pos)
    {
        int start = pos;

        // Collect roman numeral characters
        while (pos < str.Length)
        {
            char c = char.ToUpperInvariant(str[pos]);
            if (c == 'M' || c == 'D' || c == 'C' || c == 'L' || c == 'X' || c == 'V' || c == 'I')
            {
                pos++;
            }
            else
            {
                break;
            }
        }

        if (pos == start)
        {
            return -1;
        }

        string roman = str.Substring(start, pos - start);
        return (int)FromRomanNumerals(roman);
    }

    private static int ParseWordsFromString(string str, ref int pos, bool isOrdinal)
    {
        int start = pos;

        // Greedily match word characters (letters, hyphens, spaces, commas)
        int end = pos;
        while (end < str.Length)
        {
            char c = str[end];
            if (char.IsLetter(c) || c == '-' || c == ' ' || c == ',')
            {
                end++;
            }
            else
            {
                break;
            }
        }

        // Trim trailing separators
        while (end > start && (str[end - 1] == ' ' || str[end - 1] == ','))
        {
            end--;
        }

        if (end == start)
        {
            return -1;
        }

        // Try the full match first, then back off word-by-word
        int tryEnd = end;
        while (tryEnd > start)
        {
            string wordStr = str.Substring(start, tryEnd - start);

            // Try cardinal first, then ordinal (or vice versa based on isOrdinal)
            if (TryParseWordsToNumber(wordStr, isOrdinal, out long value) ||
                TryParseWordsToNumber(wordStr, !isOrdinal, out value))
            {
                pos = tryEnd;
                return (int)value;
            }

            // Back off: find the last space before tryEnd
            int lastSpace = -1;
            for (int si = tryEnd - 1; si > start; si--)
            {
                if (str[si] == ' ')
                {
                    lastSpace = si;
                    break;
                }
            }

            if (lastSpace < 0)
            {
                break;
            }

            tryEnd = lastSpace;

            // Trim trailing separators
            while (tryEnd > start && (str[tryEnd - 1] == ' ' || str[tryEnd - 1] == ','))
            {
                tryEnd--;
            }
        }

        return -1;
    }

    private static int ParseAlphaFromString(string str, ref int pos, bool uppercase)
    {
        int start = pos;
        while (pos < str.Length && char.IsLetter(str[pos]))
        {
            pos++;
        }

        if (pos == start)
        {
            return -1;
        }

        string alphaStr = str.Substring(start, pos - start);
        return (int)FromAlpha(alphaStr, uppercase);
    }

    private static int ParseFractionalSeconds(string str, ref int pos, string presentation)
    {
        int start = pos;
        while (pos < str.Length && str[pos] >= '0' && str[pos] <= '9')
        {
            pos++;
        }

        if (pos == start)
        {
            return -1;
        }

        string fracStr = str.Substring(start, pos - start);

        // Pad or truncate to 3 digits for milliseconds
        if (fracStr.Length > 3)
        {
            fracStr = fracStr.Substring(0, 3);
        }
        else if (fracStr.Length < 3)
        {
            fracStr = fracStr.PadRight(3, '0');
        }

        if (int.TryParse(fracStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            return result;
        }

        return -1;
    }

    private static string ParseAmPm(string str, ref int pos)
    {
        if (pos + 2 <= str.Length)
        {
            string twoChar = str.Substring(pos, 2);
            if (string.Equals(twoChar, "am", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(twoChar, "pm", StringComparison.OrdinalIgnoreCase))
            {
                pos += 2;
                return twoChar;
            }
        }

        return string.Empty;
    }

    private static int? ParseTimezoneOffset(string str, ref int pos)
    {
        if (pos >= str.Length)
        {
            return null;
        }

        // "Z" for UTC
        if (str[pos] == 'Z')
        {
            pos++;
            return 0;
        }

        // +HH:MM or +HHMM or +HH
        if (str[pos] != '+' && str[pos] != '-')
        {
            return null;
        }

        char sign = str[pos];
        pos++;

        // Read hours
        int hStart = pos;
        while (pos < str.Length && str[pos] >= '0' && str[pos] <= '9')
        {
            pos++;
        }

        if (pos == hStart)
        {
            return null;
        }

        string hourStr = str.Substring(hStart, pos - hStart);

        int minutes = 0;
        if (pos < str.Length && str[pos] == ':')
        {
            pos++; // skip colon
            int mStart = pos;
            while (pos < str.Length && str[pos] >= '0' && str[pos] <= '9')
            {
                pos++;
            }

            if (pos > mStart)
            {
                string minStr = str.Substring(mStart, pos - mStart);
                int.TryParse(minStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes);
            }
        }
        else if (hourStr.Length == 4)
        {
            // HHMM format
            string minStr = hourStr.Substring(2);
            hourStr = hourStr.Substring(0, 2);
            int.TryParse(minStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes);
        }

        if (!int.TryParse(hourStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hours))
        {
            return null;
        }

        int totalMinutes = (hours * 60) + minutes;
        return sign == '-' ? -totalMinutes : totalMinutes;
    }

    private static int? ParseTimezoneNameOffset(string str, ref int pos)
    {
        // Format: "GMT+HH:MM" or "GMT-HH:MM" or just "GMT"
        if (pos + 3 <= str.Length && str.Substring(pos, 3).Equals("GMT", StringComparison.OrdinalIgnoreCase))
        {
            pos += 3;
            if (pos < str.Length && (str[pos] == '+' || str[pos] == '-'))
            {
                return ParseTimezoneOffset(str, ref pos);
            }

            return 0;
        }

        // Try +/-HH:MM
        return ParseTimezoneOffset(str, ref pos);
    }

    private static void SkipDayOfWeek(string str, ref int pos, string presentation)
    {
        if (pos >= str.Length)
        {
            return;
        }

        // Check for name-based
        if (presentation == "N" || presentation == "n" || presentation == "Nn" ||
            presentation.StartsWith("Nn", StringComparison.Ordinal))
        {
            ParseNameFromString(str, ref pos, DayNames);
            return;
        }

        // Check for numeric
        if (str[pos] >= '0' && str[pos] <= '9')
        {
            while (pos < str.Length && str[pos] >= '0' && str[pos] <= '9')
            {
                pos++;
            }

            return;
        }

        // Try name anyway
        ParseNameFromString(str, ref pos, DayNames);
    }

    private static void SkipWord(string str, ref int pos)
    {
        while (pos < str.Length && char.IsLetter(str[pos]))
        {
            pos++;
        }
    }

    private static string StripWhitespace(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (!char.IsWhiteSpace(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses a timezone string (offset like "+0530" or IANA name) and returns a <see cref="TimeSpan"/> offset.
    /// </summary>
    /// <param name="timezone">The timezone string.</param>
    /// <returns>The timezone offset as a <see cref="TimeSpan"/>.</returns>
    public static TimeSpan ParseTimezoneArgument(string timezone)
    {
        if (string.IsNullOrEmpty(timezone) || timezone == "Z" || timezone == "0000")
        {
            return TimeSpan.Zero;
        }

        // Try parsing as offset: +HHMM, -HHMM, +HH:MM, -HH:MM
        if (timezone[0] == '+' || timezone[0] == '-')
        {
            return ParseOffsetString(timezone);
        }

        // Try IANA timezone name
        try
        {
            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return tz.GetUtcOffset(DateTimeOffset.UtcNow);
        }
        catch
        {
            // If nothing works, treat as zero
            return TimeSpan.Zero;
        }
    }

    private static TimeSpan ParseOffsetString(string offset)
    {
        char sign = offset[0];
        string rest = offset.Substring(1);

        int hours, minutes;

        if (rest.Contains(":"))
        {
            string[] parts = rest.Split(':');
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out hours);
            int.TryParse(parts.Length > 1 ? parts[1] : "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes);
        }
        else if (rest.Length == 4)
        {
            int.TryParse(rest.Substring(0, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out hours);
            int.TryParse(rest.Substring(2, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes);
        }
        else if (rest.Length <= 2)
        {
            int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out hours);
            minutes = 0;
        }
        else
        {
            hours = 0;
            minutes = 0;
        }

        var ts = new TimeSpan(hours, minutes, 0);
        return sign == '-' ? ts.Negate() : ts;
    }

    /// <summary>
    /// Appends an ASCII string to the UTF-8 builder, one byte per char.
    /// Only valid for strings containing only ASCII characters.
    /// </summary>
    private static void AppendAsciiString(ref Utf8ValueStringBuilder sb, string s)
    {
        Span<byte> dest = sb.AppendSpan(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            dest[i] = (byte)s[i];
        }
    }

    /// <summary>
    /// Appends a single char as UTF-8 bytes (handles BMP characters up to U+FFFF).
    /// </summary>
    private static void AppendChar(ref Utf8ValueStringBuilder sb, char c)
    {
        if (c < 0x80)
        {
            sb.Append((byte)c);
        }
        else if (c < 0x800)
        {
            sb.Append((byte)(0xC0 | (c >> 6)));
            sb.Append((byte)(0x80 | (c & 0x3F)));
        }
        else
        {
            sb.Append((byte)(0xE0 | (c >> 12)));
            sb.Append((byte)(0x80 | ((c >> 6) & 0x3F)));
            sb.Append((byte)(0x80 | (c & 0x3F)));
        }
    }
}