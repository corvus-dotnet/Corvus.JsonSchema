// <copyright file="XPathDateTimeFormatter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

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
        var sb = new StringBuilder();
        int i = 0;

        while (i < picture.Length)
        {
            if (picture[i] == '[')
            {
                if (i + 1 < picture.Length && picture[i + 1] == '[')
                {
                    sb.Append('[');
                    i += 2;
                    continue;
                }

                int end = picture.IndexOf(']', i + 1);
                if (end < 0)
                {
                    throw new JsonataException("D3135", "Picture string contains a '[' with no matching ']'", 0);
                }

                string marker = picture.Substring(i + 1, end - i - 1);
                string stripped = StripWhitespace(marker);
                FormatComponent(dt, stripped, sb);
                i = end + 1;
            }
            else if (picture[i] == ']')
            {
                if (i + 1 < picture.Length && picture[i + 1] == ']')
                {
                    sb.Append(']');
                    i += 2;
                }
                else
                {
                    sb.Append(']');
                    i++;
                }
            }
            else
            {
                sb.Append(picture[i]);
                i++;
            }
        }

        return sb.ToString();
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

            switch (compChar)
            {
                case 'Y':
                    year = ParseIntegerValueFromString(str, ref pos, presentation);
                    if (year < 0)
                    {
                        return false;
                    }

                    break;
                case 'M':
                    month = ParseDateComponentFromString(str, ref pos, presentation, MonthNames);
                    if (month < 0)
                    {
                        return false;
                    }

                    break;
                case 'D':
                    day = ParseIntegerValueFromString(str, ref pos, presentation);
                    if (day < 0)
                    {
                        return false;
                    }

                    break;
                case 'd':
                    dayOfYear = ParseIntegerValueFromString(str, ref pos, presentation);
                    if (dayOfYear < 0)
                    {
                        return false;
                    }

                    break;
                case 'H':
                    hour = ParseIntegerValueFromString(str, ref pos, presentation);
                    if (hour < 0)
                    {
                        return false;
                    }

                    break;
                case 'h':
                    hour = ParseIntegerValueFromString(str, ref pos, presentation);
                    if (hour < 0)
                    {
                        return false;
                    }

                    break;
                case 'm':
                    minute = ParseIntegerValueFromString(str, ref pos, presentation);
                    if (minute < 0)
                    {
                        return false;
                    }

                    break;
                case 's':
                    second = ParseIntegerValueFromString(str, ref pos, presentation);
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
                    throw new JsonataException("D3136", "The date/time components in the picture string are not consistent", 0);
                default:
                    throw new JsonataException("D3132", $"Unknown component specifier '{compChar}' in date/time picture string", 0);
            }
        }

        // Validate consistency
        if (dayOfYear >= 0)
        {
            if (year < 0)
            {
                throw new JsonataException("D3136", "The date/time components in the picture string are not consistent", 0);
            }

            var jan1 = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var result = jan1.AddDays(dayOfYear - 1);
            month = result.Month;
            day = result.Day;
        }

        // Validate: if we have day and year but no month (and no dayOfYear), that's an error
        if (day >= 0 && year >= 0 && month < 0 && dayOfYear < 0)
        {
            throw new JsonataException("D3136", "The date/time components in the picture string are not consistent", 0);
        }

        // Validate: if we have minute and second but no hour, that's an error
        if (hour < 0 && (minute >= 0 || second >= 0))
        {
            if (minute >= 0 && second >= 0)
            {
                throw new JsonataException("D3136", "The date/time components in the picture string are not consistent", 0);
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

        return FormatIntegerWithPresentation(value, primary, isOrdinal);
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

    internal static string FormatIntegerWithPresentation(long value, string presentation, bool isOrdinal)
    {
        // Detect the format type from the first meaningful character
        if (presentation == "I")
        {
            return ToRomanNumerals(value, true);
        }

        if (presentation == "i")
        {
            return ToRomanNumerals(value, false);
        }

        if (presentation == "W" || presentation == "w" || presentation == "Ww")
        {
            return FormatAsWords(value, presentation, isOrdinal);
        }

        if (presentation == "A")
        {
            return ToAlpha(value, true);
        }

        if (presentation == "a")
        {
            return ToAlpha(value, false);
        }

        if (presentation == "N" || presentation == "n" || presentation == "Nn")
        {
            throw new JsonataException("D3133", "The component cannot be represented as a name", 0);
        }

        // Check if it's a decimal digit pattern
        if (IsDecimalDigitPattern(presentation))
        {
            return FormatDecimalDigit(value, presentation, isOrdinal);
        }

        // Check for single '#' which is an error in formatInteger context
        if (presentation == "#")
        {
            throw new JsonataException("D3130", "The format/picture string is not valid", 0);
        }

        throw new JsonataException("D3130", "The format/picture string is not valid", 0);
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
            throw new JsonataException("D3130", "The format/picture string is not valid", 0);
        }

        if (IsDecimalDigitPattern(presentation))
        {
            return TryParseDecimalDigit(str, presentation, isOrdinal, out value);
        }

        throw new JsonataException("D3130", "The format/picture string is not valid", 0);
    }

    private static void FormatComponent(DateTimeOffset dt, string marker, StringBuilder sb)
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

        if (widthCommaIdx >= 0)
        {
            string widthSpec = rest.Substring(widthCommaIdx + 1);
            rest = rest.Substring(0, widthCommaIdx);
            ParseWidthModifier(widthSpec, out minWidth, out maxWidth);
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
                FormatDateValue(dt.Year, presentation, minWidth, maxWidth, sb);
                break;
            case 'M':
                FormatDateValueOrName(dt.Month, presentation, minWidth, maxWidth, MonthNames, sb);
                break;
            case 'D':
                FormatDateValue(dt.Day, presentation, minWidth, maxWidth, sb);
                break;
            case 'd':
                FormatDateValue(dt.DayOfYear, presentation, minWidth, maxWidth, sb);
                break;
            case 'F':
                int isoDay = GetIsoDayOfWeek(dt.DayOfWeek);
                FormatDateValueOrName(isoDay, presentation, minWidth, maxWidth, DayNames, sb);
                break;
            case 'H':
                FormatDateValue(dt.Hour, presentation, minWidth, maxWidth, sb);
                break;
            case 'h':
                int h12 = dt.Hour % 12;
                if (h12 == 0)
                {
                    h12 = 12;
                }

                FormatDateValue(h12, presentation, minWidth, maxWidth, sb);
                break;
            case 'P':
                string ampm = dt.Hour < 12 ? "am" : "pm";
                if (presentation.Length == 0 || presentation == "n")
                {
                    sb.Append(ampm);
                }
                else if (presentation == "N")
                {
                    sb.Append(ampm.ToUpperInvariant());
                }
                else
                {
                    sb.Append(ampm);
                }

                break;
            case 'm':
                FormatDateValue(dt.Minute, presentation, minWidth, maxWidth, sb);
                break;
            case 's':
                FormatDateValue(dt.Second, presentation, minWidth, maxWidth, sb);
                break;
            case 'f':
                FormatFractionalSeconds(dt.Millisecond, presentation, sb);
                break;
            case 'Z':
                FormatTimezoneOffset(dt.Offset, presentation, sb);
                break;
            case 'z':
                FormatTimezoneGmt(dt.Offset, sb);
                break;
            case 'W':
                FormatDateValue(GetIsoWeekOfYear(dt), presentation, minWidth, maxWidth, sb);
                break;
            case 'w':
                FormatDateValue(GetWeekOfMonth(dt), presentation, minWidth, maxWidth, sb);
                break;
            case 'x':
                FormatDateValueOrName(GetMonthOfWeek(dt), presentation, minWidth, maxWidth, MonthNames, sb);
                break;
            case 'X':
                FormatDateValue(GetIsoWeekYear(dt), presentation, minWidth, maxWidth, sb);
                break;
            case 'C':
                sb.Append("ISO");
                break;
            case 'E':
                sb.Append("ISO");
                break;
            default:
                throw new JsonataException("D3132", $"Unknown component specifier '{comp}' in date/time picture string", 0);
        }
    }

    private static void FormatDateValue(int value, string presentation, int minWidth, int maxWidth, StringBuilder sb)
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

        // Name presentations (N, n, Nn) only apply to components with names (month, day-of-week).
        // For numeric-only components (year, hour, minute, etc.), fall back to default numeric.
        if (pres == "N" || pres == "n" || pres == "Nn")
        {
            pres = "1";
        }

        string formatted = FormatIntegerWithPresentation(value, pres, isOrdinal);

        if (maxWidth >= 0 && formatted.Length > maxWidth)
        {
            formatted = formatted.Substring(formatted.Length - maxWidth);
        }

        if (minWidth >= 0 && formatted.Length < minWidth)
        {
            formatted = formatted.PadLeft(minWidth, '0');
        }

        sb.Append(formatted);
    }

    private static void FormatDateValueOrName(int value, string presentation, int minWidth, int maxWidth, string[] names, StringBuilder sb)
    {
        if (presentation.Length == 0)
        {
            // Default numeric
            FormatDateValue(value, presentation, minWidth, maxWidth, sb);
            return;
        }

        // Check for name presentations
        bool isName = false;
        bool isUpper = false;
        bool isLower = false;
        bool isTitleCase = false;

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
            isTitleCase = true;
        }

        if (isName && names.Length > 0)
        {
            int idx = value - 1;
            if (idx < 0 || idx >= names.Length)
            {
                sb.Append(value.ToString(CultureInfo.InvariantCulture));
                return;
            }

            string name = names[idx];

            if (maxWidth >= 0 && name.Length > maxWidth)
            {
                name = name.Substring(0, maxWidth);
            }

            if (isUpper)
            {
                name = name.ToUpperInvariant();
            }
            else if (isLower)
            {
                name = name.ToLowerInvariant();
            }
            else if (isTitleCase)
            {
                // Already title case
            }

            sb.Append(name);
            return;
        }

        FormatDateValue(value, presentation, minWidth, maxWidth, sb);
    }

    private static void FormatFractionalSeconds(int milliseconds, string presentation, StringBuilder sb)
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

        string ms = milliseconds.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0');

        if (digits <= 3)
        {
            sb.Append(ms.Substring(0, digits));
        }
        else
        {
            sb.Append(ms);
            for (int i = 3; i < digits; i++)
            {
                sb.Append('0');
            }
        }
    }

    private static void FormatTimezoneOffset(TimeSpan offset, string presentation, StringBuilder sb)
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
            sb.Append('Z');
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
            throw new JsonataException("D3134", "The timezone component of the picture string is not valid", 0);
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

        char sign = offset >= TimeSpan.Zero ? '+' : '-';
        sb.Append(sign);

        int totalMinutes = (int)Math.Abs(offset.TotalMinutes);
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;

        if (digits == 0)
        {
            // Minimal format
            sb.Append(hours.ToString(CultureInfo.InvariantCulture));
            if (minutes > 0)
            {
                sb.Append(':');
                sb.Append(minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
            }
        }
        else
        {
            sb.Append(hours.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
            if (useColon)
            {
                sb.Append(':');
            }

            sb.Append(minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
        }
    }

    private static void FormatTimezoneGmt(TimeSpan offset, StringBuilder sb)
    {
        sb.Append("GMT");
        if (offset != TimeSpan.Zero)
        {
            char sign = offset >= TimeSpan.Zero ? '+' : '-';
            sb.Append(sign);
            int totalMinutes = (int)Math.Abs(offset.TotalMinutes);
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            sb.Append(hours.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
            sb.Append(':');
            sb.Append(minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
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
                    throw new JsonataException("D3131", "The format/picture string contains mixed decimal digit groups", 0);
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
                    throw new JsonataException("D3131", "The format/picture string contains mixed decimal digit groups", 0);
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

    private static string FormatDecimalDigit(long value, string presentation, bool isOrdinal)
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
        var groupSeparators = new List<(int Position, char Separator)>();
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
                else
                {
                    // Grouping separator
                    groupSeparators.Add((digitCount, c));
                }
            }
        }

        if (mandatoryDigits == 0)
        {
            mandatoryDigits = 1;
        }

        string digits = absValue.ToString(CultureInfo.InvariantCulture);
        if (digits.Length < mandatoryDigits)
        {
            digits = digits.PadLeft(mandatoryDigits, '0');
        }

        // Convert to unicode if needed
        if (unicodeBase != 0)
        {
            var unicodeDigits = new char[digits.Length];
            for (int i = 0; i < digits.Length; i++)
            {
                unicodeDigits[i] = (char)(unicodeBase + (digits[i] - '0'));
            }

            digits = new string(unicodeDigits);
        }

        // Insert grouping separators
        if (groupSeparators.Count > 0)
        {
            digits = InsertGroupingSeparators(digits, groupSeparators);
        }

        var result = new StringBuilder();
        if (isNegative)
        {
            result.Append('-');
        }

        result.Append(digits);

        if (isOrdinal)
        {
            result.Append(GetOrdinalSuffix(absValue));
        }

        return result.ToString();
    }

    private static string InsertGroupingSeparators(string digits, List<(int Position, char Separator)> separators)
    {
        if (separators.Count == 0)
        {
            return digits;
        }

        // Check if all separators use the same character and are regularly spaced
        bool isRegular = true;
        char sepChar = separators[0].Separator;
        int groupSize = separators[0].Position;

        for (int i = 1; i < separators.Count; i++)
        {
            if (separators[i].Separator != sepChar)
            {
                isRegular = false;
                break;
            }

            // Check regular spacing from the previous separator
            int expectedPos = separators[0].Position + (i * groupSize);

            // Actually, let's re-check: positions are cumulative digit counts
            // separators[0].Position = digits from right to first separator
            // separators[1].Position = digits from right to second separator
            // Regular if separators[i].Position = groupSize * (i + 1)
            if (separators[i].Position != groupSize * (i + 1))
            {
                isRegular = false;
                break;
            }
        }

        var result = new StringBuilder();

        if (isRegular && separators.Count > 0)
        {
            // Regular grouping - repeat for the entire number
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                result.Insert(0, digits[i]);
                int posFromRight = digits.Length - i;
                if (posFromRight % groupSize == 0 && i > 0)
                {
                    result.Insert(0, sepChar);
                }
            }
        }
        else
        {
            // Non-regular grouping: use explicit positions from the pattern
            // Sort separators by position ascending
            var sortedSeps = new List<(int Position, char Separator)>(separators);
            sortedSeps.Sort((a, b) => a.Position.CompareTo(b.Position));

            // Build from right to left
            int digitIdx = digits.Length - 1;
            int currentPos = 0;
            int sepIdx = 0;
            var parts = new List<char>();

            while (digitIdx >= 0)
            {
                parts.Add(digits[digitIdx]);
                currentPos++;
                digitIdx--;

                if (sepIdx < sortedSeps.Count && currentPos == sortedSeps[sepIdx].Position && digitIdx >= 0)
                {
                    parts.Add(sortedSeps[sepIdx].Separator);
                    sepIdx++;
                }
            }

            parts.Reverse();
            foreach (char c in parts)
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    private static string GetOrdinalSuffix(long value)
    {
        long lastTwo = value % 100;
        long lastOne = value % 10;

        if (lastTwo >= 11 && lastTwo <= 13)
        {
            return "th";
        }

        return lastOne switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th",
        };
    }

    private static readonly (string Symbol, int Value)[] RomanValues =
    {
        ("M", 1000), ("CM", 900), ("D", 500), ("CD", 400),
        ("C", 100), ("XC", 90), ("L", 50), ("XL", 40),
        ("X", 10), ("IX", 9), ("V", 5), ("IV", 4), ("I", 1),
    };

    private static string ToRomanNumerals(long value, bool uppercase)
    {
        if (value <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        long remaining = value;

        foreach (var (symbol, val) in RomanValues)
        {
            while (remaining >= val)
            {
                sb.Append(symbol);
                remaining -= val;
            }
        }

        string result = sb.ToString();
        return uppercase ? result : result.ToLowerInvariant();
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

    private static string ToAlpha(long value, bool uppercase)
    {
        if (value <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        long remaining = value;
        char baseChar = uppercase ? 'A' : 'a';

        while (remaining > 0)
        {
            remaining--;
            sb.Insert(0, (char)(baseChar + (remaining % 26)));
            remaining /= 26;
        }

        return sb.ToString();
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

        return TryParseCardinalWords(text, out value);
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

    private static bool TryParseCardinalWords(string text, out long value)
    {
        value = 0;

        // Tokenize: split on spaces, commas, "and"
        string cleaned = text.Replace(",", " ").Replace(" and ", " ");
        string[] tokens = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
        {
            return false;
        }

        // Parse using a stack-based approach for scale words
        long current = 0;
        long result = 0;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];

            // Check hyphenated words like "twenty-three"
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

            // Check scale words
            long scaleVal = GetScaleValue(token);
            if (scaleVal > 0)
            {
                if (scaleVal >= 1000)
                {
                    // Major scale: multiply accumulated and add to result
                    if (current == 0)
                    {
                        current = 1;
                    }

                    current *= scaleVal;
                    result += current;
                    current = 0;
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
                    throw new JsonataException("D3135", "Picture string contains a '[' with no matching ']'", 0);
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

                // Remove width modifier
                int comma = pres.IndexOf(',');
                if (comma >= 0)
                {
                    pres = pres.Substring(0, comma);
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
                        throw new JsonataException("D3133", "The year component cannot be represented as a name", 0);
                    }

                    components.Add(new PictureComponent
                    {
                        IsLiteral = false,
                        Component = comp,
                        Presentation = pres,
                    });
                }
                else
                {
                    throw new JsonataException("D3132", $"Unknown component specifier '{comp}' in date/time picture string", 0);
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

    private static int ParseIntegerValueFromString(string str, ref int pos, string presentation)
    {
        if (pos >= str.Length)
        {
            return -1;
        }

        // Check for 'N' or 'Nn' name presentation (used for year names - not supported)
        if (presentation == "N" || presentation == "Nn")
        {
            throw new JsonataException("D3133", "The component cannot be represented as a name", 0);
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

        // Parse numeric value
        int start = pos;
        bool negative = false;
        if (pos < str.Length && str[pos] == '-')
        {
            negative = true;
            pos++;
        }

        while (pos < str.Length && str[pos] >= '0' && str[pos] <= '9')
        {
            pos++;
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

    private static int ParseDateComponentFromString(string str, ref int pos, string presentation, string[] names)
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
        return ParseIntegerValueFromString(str, ref pos, presentation);
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
        // Find the extent of the word(s) - consume until we hit something that's clearly not a word char
        int start = pos;
        int lastGoodEnd = pos;

        // Greedily match word characters (letters, hyphens, spaces, commas)
        while (pos < str.Length)
        {
            char c = str[pos];
            if (char.IsLetter(c) || c == '-' || c == ' ' || c == ',')
            {
                pos++;
            }
            else
            {
                break;
            }
        }

        // Trim trailing spaces/commas
        while (pos > start && (str[pos - 1] == ' ' || str[pos - 1] == ','))
        {
            pos--;
        }

        if (pos == start)
        {
            return -1;
        }

        string wordStr = str.Substring(start, pos - start);
        if (TryParseWordsToNumber(wordStr, isOrdinal, out long value))
        {
            return (int)value;
        }

        pos = start;
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
}