// <copyright file="XPathDateTimeFormatter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Buffers;
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
    private static readonly byte[][] MonthNames =
    {
        "January"u8.ToArray(), "February"u8.ToArray(), "March"u8.ToArray(),
        "April"u8.ToArray(), "May"u8.ToArray(), "June"u8.ToArray(),
        "July"u8.ToArray(), "August"u8.ToArray(), "September"u8.ToArray(),
        "October"u8.ToArray(), "November"u8.ToArray(), "December"u8.ToArray(),
    };

    private static readonly byte[][] DayNames =
    {
        "Monday"u8.ToArray(), "Tuesday"u8.ToArray(), "Wednesday"u8.ToArray(),
        "Thursday"u8.ToArray(), "Friday"u8.ToArray(), "Saturday"u8.ToArray(), "Sunday"u8.ToArray(),
    };

    private static readonly byte[][] Ones =
    {
        Array.Empty<byte>(), "one"u8.ToArray(), "two"u8.ToArray(), "three"u8.ToArray(),
        "four"u8.ToArray(), "five"u8.ToArray(), "six"u8.ToArray(), "seven"u8.ToArray(),
        "eight"u8.ToArray(), "nine"u8.ToArray(), "ten"u8.ToArray(), "eleven"u8.ToArray(),
        "twelve"u8.ToArray(), "thirteen"u8.ToArray(), "fourteen"u8.ToArray(), "fifteen"u8.ToArray(),
        "sixteen"u8.ToArray(), "seventeen"u8.ToArray(), "eighteen"u8.ToArray(), "nineteen"u8.ToArray(),
    };

    private static readonly byte[][] Tens =
    {
        Array.Empty<byte>(), Array.Empty<byte>(), "twenty"u8.ToArray(), "thirty"u8.ToArray(),
        "forty"u8.ToArray(), "fifty"u8.ToArray(), "sixty"u8.ToArray(), "seventy"u8.ToArray(),
        "eighty"u8.ToArray(), "ninety"u8.ToArray(),
    };

    private static readonly (byte[] Key, byte[] Value)[] OrdinalWordMap =
    {
        ("one"u8.ToArray(), "first"u8.ToArray()),
        ("two"u8.ToArray(), "second"u8.ToArray()),
        ("three"u8.ToArray(), "third"u8.ToArray()),
        ("four"u8.ToArray(), "fourth"u8.ToArray()),
        ("five"u8.ToArray(), "fifth"u8.ToArray()),
        ("six"u8.ToArray(), "sixth"u8.ToArray()),
        ("seven"u8.ToArray(), "seventh"u8.ToArray()),
        ("eight"u8.ToArray(), "eighth"u8.ToArray()),
        ("nine"u8.ToArray(), "ninth"u8.ToArray()),
        ("ten"u8.ToArray(), "tenth"u8.ToArray()),
        ("eleven"u8.ToArray(), "eleventh"u8.ToArray()),
        ("twelve"u8.ToArray(), "twelfth"u8.ToArray()),
        ("thirteen"u8.ToArray(), "thirteenth"u8.ToArray()),
        ("fourteen"u8.ToArray(), "fourteenth"u8.ToArray()),
        ("fifteen"u8.ToArray(), "fifteenth"u8.ToArray()),
        ("sixteen"u8.ToArray(), "sixteenth"u8.ToArray()),
        ("seventeen"u8.ToArray(), "seventeenth"u8.ToArray()),
        ("eighteen"u8.ToArray(), "eighteenth"u8.ToArray()),
        ("nineteen"u8.ToArray(), "nineteenth"u8.ToArray()),
        ("twenty"u8.ToArray(), "twentieth"u8.ToArray()),
        ("thirty"u8.ToArray(), "thirtieth"u8.ToArray()),
        ("forty"u8.ToArray(), "fortieth"u8.ToArray()),
        ("fifty"u8.ToArray(), "fiftieth"u8.ToArray()),
        ("sixty"u8.ToArray(), "sixtieth"u8.ToArray()),
        ("seventy"u8.ToArray(), "seventieth"u8.ToArray()),
        ("eighty"u8.ToArray(), "eightieth"u8.ToArray()),
        ("ninety"u8.ToArray(), "ninetieth"u8.ToArray()),
        ("hundred"u8.ToArray(), "hundredth"u8.ToArray()),
        ("thousand"u8.ToArray(), "thousandth"u8.ToArray()),
        ("million"u8.ToArray(), "millionth"u8.ToArray()),
        ("billion"u8.ToArray(), "billionth"u8.ToArray()),
        ("trillion"u8.ToArray(), "trillionth"u8.ToArray()),
    };

    private static readonly (byte[] Name, long Value)[] ScaleWords =
    {
        ("trillion"u8.ToArray(), 1_000_000_000_000L),
        ("billion"u8.ToArray(), 1_000_000_000L),
        ("million"u8.ToArray(), 1_000_000L),
        ("thousand"u8.ToArray(), 1_000L),
        ("hundred"u8.ToArray(), 100L),
    };

    /// <summary>
    /// Formats the given <see cref="DateTimeOffset"/> using the XPath picture string,
    /// writing the UTF-8 result directly to a caller-supplied buffer.
    /// </summary>
    /// <param name="dt">The date and time to format.</param>
    /// <param name="picture">The XPath picture string.</param>
    /// <param name="destination">The destination buffer for UTF-8 output.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the destination was large enough; otherwise <see langword="false"/>.</returns>
    internal static bool TryFormatDateTime(DateTimeOffset dt, ReadOnlySpan<byte> picture, Span<byte> destination, out int bytesWritten)
    {
        Utf8ValueStringBuilder sb = new(destination);
        FormatDateTime(dt, picture, ref sb);
        bool success = sb.TryCopyTo(destination, out bytesWritten);
        sb.Dispose();
        return success;
    }

    internal static void FormatDateTime(DateTimeOffset dt, ReadOnlySpan<byte> picture, ref Utf8ValueStringBuilder sb)
    {
        ValidateBrackets(picture);

        int i = 0;

        // Rent a small buffer for whitespace stripping (heap-backed span avoids
        // ref-safety conflicts with the ref Utf8ValueStringBuilder parameter).
        byte[] stripRented = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            while (i < picture.Length)
            {
                if (picture[i] == (byte)'[')
                {
                    if (i + 1 < picture.Length && picture[i + 1] == (byte)'[')
                    {
                        sb.Append((byte)'[');
                        i += 2;
                        continue;
                    }

                    int endRel = picture.Slice(i + 1).IndexOf((byte)']');
                    if (endRel < 0)
                    {
                        throw new JsonataException("D3135", SR.D3135_PictureStringContainsAWithNoMatching, 0);
                    }

                    int end = i + 1 + endRel;
                    ReadOnlySpan<byte> marker = picture.Slice(i + 1, end - i - 1);
                    int strippedLen = StripAllAsciiWhitespace(marker, stripRented);
                    FormatComponent(dt, ((ReadOnlySpan<byte>)stripRented).Slice(0, strippedLen), ref sb);
                    i = end + 1;
                }
                else if (picture[i] == (byte)']')
                {
                    if (i + 1 < picture.Length && picture[i + 1] == (byte)']')
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
                    sb.Append(picture[i]);
                    i++;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(stripRented);
        }
    }

    private static void ValidateBrackets(ReadOnlySpan<byte> picture)
    {
        int i = 0;
        while (i < picture.Length)
        {
            if (picture[i] == (byte)'[')
            {
                if (i + 1 < picture.Length && picture[i + 1] == (byte)'[')
                {
                    i += 2;
                    continue;
                }

                int end = picture.Slice(i + 1).IndexOf((byte)']');
                if (end < 0)
                {
                    throw new JsonataException("D3135", SR.D3135_PictureStringContainsAWithNoMatching, 0);
                }

                i = i + 1 + end + 1;
            }
            else
            {
                i++;
            }
        }
    }

    /// <summary>
    /// Parses a date/time UTF-8 byte span using an XPath picture string and returns UTC milliseconds since epoch.
    /// </summary>
    /// <param name="utf8">The UTF-8 bytes to parse.</param>
    /// <param name="picture">The XPath picture string.</param>
    /// <param name="millis">The parsed milliseconds since Unix epoch.</param>
    /// <returns><see langword="true"/> if the string was successfully parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParseDateTime(ReadOnlySpan<byte> utf8, ReadOnlySpan<byte> picture, out long millis)
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
                byte[] litBytes = comp.Literal!;
                if (pos + litBytes.Length > utf8.Length)
                {
                    return false;
                }

                // Match literal text (case-insensitive for ASCII)
                if (!Utf8EqualsAsciiIgnoreCase(utf8.Slice(pos, litBytes.Length), litBytes))
                {
                    return false;
                }

                pos += litBytes.Length;
                continue;
            }

            byte compChar = comp.Component;
            byte[] presentation = comp.Presentation;
            int digitWidth = comp.DigitWidth;

            switch (compChar)
            {
                case (byte)'Y':
                    year = ParseIntegerValue(utf8, ref pos, presentation, digitWidth);
                    if (year < 0)
                    {
                        return false;
                    }

                    break;
                case (byte)'M':
                    month = ParseDateComponent(utf8, ref pos, presentation, MonthNames, digitWidth);
                    if (month < 0)
                    {
                        return false;
                    }

                    break;
                case (byte)'D':
                    day = ParseIntegerValue(utf8, ref pos, presentation, digitWidth);
                    if (day < 0)
                    {
                        return false;
                    }

                    break;
                case (byte)'d':
                    dayOfYear = ParseIntegerValue(utf8, ref pos, presentation, digitWidth);
                    if (dayOfYear < 0)
                    {
                        return false;
                    }

                    break;
                case (byte)'H':
                    hour = ParseIntegerValue(utf8, ref pos, presentation, digitWidth);
                    if (hour < 0)
                    {
                        return false;
                    }

                    break;
                case (byte)'h':
                    hour = ParseIntegerValue(utf8, ref pos, presentation, digitWidth);
                    if (hour < 0)
                    {
                        return false;
                    }

                    break;
                case (byte)'m':
                    minute = ParseIntegerValue(utf8, ref pos, presentation, digitWidth);
                    if (minute < 0)
                    {
                        return false;
                    }

                    break;
                case (byte)'s':
                    second = ParseIntegerValue(utf8, ref pos, presentation, digitWidth);
                    if (second < 0)
                    {
                        return false;
                    }

                    break;
                case (byte)'f':
                    millisecond = ParseFractionalSeconds(utf8, ref pos);
                    if (millisecond < 0)
                    {
                        return false;
                    }

                    break;
                case (byte)'P':
                    // AM/PM: 0=none, 1=am, 2=pm
                    int ampmResult = ParseAmPm(utf8, ref pos);
                    if (ampmResult == 0)
                    {
                        return false;
                    }

                    if (ampmResult == 2)
                    {
                        if (hour >= 0 && hour < 12)
                        {
                            hour += 12;
                        }
                    }
                    else if (ampmResult == 1)
                    {
                        if (hour == 12)
                        {
                            hour = 0;
                        }
                    }

                    break;
                case (byte)'Z':
                    tzOffsetMinutes = ParseTimezoneOffset(utf8, ref pos);
                    break;
                case (byte)'z':
                    tzOffsetMinutes = ParseTimezoneNameOffset(utf8, ref pos);
                    break;
                case (byte)'F':
                    // Day of week - just consume it, we don't use it for calculation
                    SkipDayOfWeek(utf8, ref pos, presentation);
                    break;
                case (byte)'C':
                case (byte)'E':
                    // Calendar/Era - skip
                    SkipWord(utf8, ref pos);
                    break;
                case (byte)'W':
                case (byte)'w':
                case (byte)'X':
                case (byte)'x':
                    throw new JsonataException("D3136", SR.D3136_TheDateTimeComponentsInThePictureStringAreNotConsistent, 0);
                default:
                    throw new JsonataException("D3132", SR.Format(SR.D3132_UnknownComponentSpecifier, (char)compChar), 0);
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
    /// Formats an integer using the XPath picture string,
    /// writing the UTF-8 result directly to a caller-supplied buffer.
    /// </summary>
    /// <param name="value">The integer value to format.</param>
    /// <param name="picture">The XPath integer picture string.</param>
    /// <param name="destination">The destination buffer for UTF-8 output.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the destination was large enough; otherwise <see langword="false"/>.</returns>
    internal static bool TryFormatInteger(long value, ReadOnlySpan<byte> picture, Span<byte> destination, out int bytesWritten)
    {
        Utf8ValueStringBuilder sb = new(destination);
        FormatInteger(value, picture, ref sb);
        bool success = sb.TryCopyTo(destination, out bytesWritten);
        sb.Dispose();
        return success;
    }

    internal static void FormatInteger(long value, ReadOnlySpan<byte> picture, ref Utf8ValueStringBuilder sb)
    {
        // Split picture on ';' for ordinal modifier
        ReadOnlySpan<byte> primary;
        bool isOrdinal = false;
        int semiIdx = picture.IndexOf((byte)';');
        if (semiIdx >= 0)
        {
            primary = picture.Slice(0, semiIdx);
            ReadOnlySpan<byte> modifier = picture.Slice(semiIdx + 1);
            if (modifier.IndexOf((byte)'o') >= 0)
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
            primary = "0"u8;
        }

        FormatIntegerWithPresentation(value, primary, isOrdinal, ref sb);
    }

    /// <summary>
    /// Formats a large integer (as a double) using an XPath integer picture string.
    /// Used when the value is outside the range of <see cref="long"/>.
    /// </summary>
    /// <param name="value">The integer value as a double.</param>
    /// <param name="picture">The XPath integer picture string.</param>
    /// <param name="sb">The UTF-8 builder to append to.</param>
    internal static void FormatInteger(double value, ReadOnlySpan<byte> picture, ref Utf8ValueStringBuilder sb)
    {
        if (value >= long.MinValue && value <= long.MaxValue)
        {
            FormatInteger((long)value, picture, ref sb);
            return;
        }

        ReadOnlySpan<byte> primary;
        bool isOrdinal = false;
        int semiIdx = picture.IndexOf((byte)';');
        if (semiIdx >= 0)
        {
            primary = picture.Slice(0, semiIdx);
            ReadOnlySpan<byte> modifier = picture.Slice(semiIdx + 1);
            if (modifier.IndexOf((byte)'o') >= 0)
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
            primary = "0"u8;
        }

        if (primary.SequenceEqual("W"u8) || primary.SequenceEqual("w"u8) || primary.SequenceEqual("Ww"u8))
        {
            AppendAsWordsLarge(value, primary, isOrdinal, ref sb);
        }
        else
        {
            // For non-word patterns, format as decimal digits via Utf8Formatter
            Span<byte> buf = sb.AppendSpan(32);
            Utf8Formatter.TryFormat(value, buf, out int written, new StandardFormat('R'));
            sb.Length -= 32 - written;
        }
    }

    /// <summary>
    /// Parses an integer from a UTF-8 byte span using an XPath integer picture string.
    /// </summary>
    /// <param name="utf8">The UTF-8 bytes to parse.</param>
    /// <param name="picture">The XPath integer picture string.</param>
    /// <param name="value">The parsed value.</param>
    /// <returns><see langword="true"/> if the string was successfully parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParseInteger(ReadOnlySpan<byte> utf8, ReadOnlySpan<byte> picture, out long value)
    {
        value = 0;

        ReadOnlySpan<byte> primary;
        bool isOrdinal = false;
        int semiIdx = picture.IndexOf((byte)';');
        if (semiIdx >= 0)
        {
            primary = picture.Slice(0, semiIdx);
            ReadOnlySpan<byte> modifier = picture.Slice(semiIdx + 1);
            if (modifier.IndexOf((byte)'o') >= 0)
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
            primary = "0"u8;
        }

        return TryParseIntegerWithPresentation(utf8, primary, isOrdinal, out value);
    }

    /// <summary>
    /// Parses an integer from a UTF-8 byte span using an XPath integer picture string, returning a double
    /// to handle values outside the range of <see cref="long"/>.
    /// </summary>
    /// <param name="utf8">The UTF-8 bytes to parse.</param>
    /// <param name="picture">The XPath integer picture string.</param>
    /// <param name="value">The parsed value as a double.</param>
    /// <returns><see langword="true"/> if the string was successfully parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParseInteger(ReadOnlySpan<byte> utf8, ReadOnlySpan<byte> picture, out double value)
    {
        value = 0;

        ReadOnlySpan<byte> primary;
        bool isOrdinal = false;
        int semiIdx = picture.IndexOf((byte)';');
        if (semiIdx >= 0)
        {
            primary = picture.Slice(0, semiIdx);
            ReadOnlySpan<byte> modifier = picture.Slice(semiIdx + 1);
            if (modifier.IndexOf((byte)'o') >= 0)
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
            primary = "0"u8;
        }

        if (primary.SequenceEqual("W"u8) || primary.SequenceEqual("w"u8) || primary.SequenceEqual("Ww"u8))
        {
            return TryParseWordsToNumberDouble(utf8, isOrdinal, out value);
        }

        // For other patterns, use the long version
        if (TryParseIntegerWithPresentation(utf8, primary, isOrdinal, out long longValue))
        {
            value = longValue;
            return true;
        }

        return false;
    }

    internal static void FormatIntegerWithPresentation(long value, ReadOnlySpan<byte> presentation, bool isOrdinal, ref Utf8ValueStringBuilder result)
    {
        // Detect the format type from the first meaningful character
        if (presentation.SequenceEqual("I"u8))
        {
            ToRomanNumerals(value, true, ref result);
            return;
        }

        if (presentation.SequenceEqual("i"u8))
        {
            ToRomanNumerals(value, false, ref result);
            return;
        }

        if (presentation.SequenceEqual("W"u8) || presentation.SequenceEqual("w"u8) || presentation.SequenceEqual("Ww"u8))
        {
            AppendAsWords(value, presentation, isOrdinal, ref result);
            return;
        }

        if (presentation.SequenceEqual("A"u8))
        {
            ToAlpha(value, true, ref result);
            return;
        }

        if (presentation.SequenceEqual("a"u8))
        {
            ToAlpha(value, false, ref result);
            return;
        }

        if (presentation.SequenceEqual("N"u8) || presentation.SequenceEqual("n"u8) || presentation.SequenceEqual("Nn"u8))
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
        if (presentation.SequenceEqual("#"u8))
        {
            throw new JsonataException("D3130", SR.D3130_TheFormatPictureStringIsNotValid, 0);
        }

        throw new JsonataException("D3130", SR.D3130_TheFormatPictureStringIsNotValid, 0);
    }

    private static bool TryParseIntegerWithPresentation(ReadOnlySpan<byte> utf8, ReadOnlySpan<byte> presentation, bool isOrdinal, out long value)
    {
        value = 0;

        if (presentation.SequenceEqual("I"u8) || presentation.SequenceEqual("i"u8))
        {
            value = FromRomanNumerals(utf8);
            return true;
        }

        if (presentation.SequenceEqual("W"u8) || presentation.SequenceEqual("w"u8) || presentation.SequenceEqual("Ww"u8))
        {
            return TryParseWordsToNumber(utf8, isOrdinal, out value);
        }

        if (presentation.SequenceEqual("A"u8))
        {
            value = FromAlpha(utf8, true);
            return true;
        }

        if (presentation.SequenceEqual("a"u8))
        {
            value = FromAlpha(utf8, false);
            return true;
        }

        if (presentation.SequenceEqual("#"u8))
        {
            throw new JsonataException("D3130", SR.D3130_TheFormatPictureStringIsNotValid, 0);
        }

        if (IsDecimalDigitPattern(presentation))
        {
            return TryParseDecimalDigit(utf8, presentation, isOrdinal, out value);
        }

        throw new JsonataException("D3130", SR.D3130_TheFormatPictureStringIsNotValid, 0);
    }

    private static void FormatComponent(DateTimeOffset dt, ReadOnlySpan<byte> marker, ref Utf8ValueStringBuilder sb)
    {
        if (marker.Length == 0)
        {
            return;
        }

        byte comp = marker[0];
        ReadOnlySpan<byte> rest = marker.Length > 1 ? marker.Slice(1) : ReadOnlySpan<byte>.Empty;

        // Parse optional width modifier (after comma).
        // The presentation modifier can itself contain commas (e.g. '#,##0' as a grouping pattern),
        // so we scan from right to left for the comma that starts a valid width modifier.
        int maxWidth = -1;
        int minWidth = -1;
        int widthCommaIdx = -1;
        for (int ci = rest.Length - 1; ci >= 0; ci--)
        {
            if (rest[ci] == (byte)',')
            {
                ReadOnlySpan<byte> candidate = rest.Slice(ci + 1);
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
            ReadOnlySpan<byte> widthSpec = rest.Slice(widthCommaIdx + 1);
            rest = rest.Slice(0, widthCommaIdx);
            bool isRange = widthSpec.IndexOf((byte)'-') >= 0;
            ParseWidthModifier(widthSpec, out minWidth, out maxWidth);
            hardMaxWidth = isRange;

            if (!isRange && maxWidth >= 0)
            {
                int mandatoryFromPres = CountMandatoryDigits(rest);
                if (mandatoryFromPres > maxWidth)
                {
                    maxWidth = mandatoryFromPres;
                }
            }
        }

        ReadOnlySpan<byte> presentation = rest;

        // Apply XPath 3.1 default presentation modifiers per component
        if (presentation.Length == 0)
        {
            presentation = comp switch
            {
                (byte)'Y' => "1"u8,
                (byte)'M' => "1"u8,
                (byte)'D' => "1"u8,
                (byte)'d' => "1"u8,
                (byte)'F' => "n"u8,
                (byte)'H' => "1"u8,
                (byte)'h' => "1"u8,
                (byte)'P' => "n"u8,
                (byte)'m' => "01"u8,
                (byte)'s' => "01"u8,
                (byte)'f' => "1"u8,
                (byte)'Z' => "01:01"u8,
                (byte)'W' => "1"u8,
                (byte)'w' => "1"u8,
                (byte)'x' => "1"u8,
                (byte)'X' => "1"u8,
                _ => "1"u8,
            };
        }

        switch (comp)
        {
            case (byte)'Y':
                FormatDateValue(dt.Year, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case (byte)'M':
                FormatDateValueOrName(dt.Month, presentation, minWidth, maxWidth, hardMaxWidth, MonthNames, ref sb);
                break;
            case (byte)'D':
                FormatDateValue(dt.Day, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case (byte)'d':
                FormatDateValue(dt.DayOfYear, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case (byte)'F':
                int isoDay = GetIsoDayOfWeek(dt.DayOfWeek);
                FormatDateValueOrName(isoDay, presentation, minWidth, maxWidth, hardMaxWidth, DayNames, ref sb);
                break;
            case (byte)'H':
                FormatDateValue(dt.Hour, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case (byte)'h':
                int h12 = dt.Hour % 12;
                if (h12 == 0)
                {
                    h12 = 12;
                }

                FormatDateValue(h12, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case (byte)'P':
                if (presentation.Length == 0 || presentation.SequenceEqual("n"u8))
                {
                    sb.Append(dt.Hour < 12 ? "am"u8 : "pm"u8);
                }
                else if (presentation.SequenceEqual("N"u8))
                {
                    sb.Append(dt.Hour < 12 ? "AM"u8 : "PM"u8);
                }
                else
                {
                    sb.Append(dt.Hour < 12 ? "am"u8 : "pm"u8);
                }

                break;
            case (byte)'m':
                FormatDateValue(dt.Minute, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case (byte)'s':
                FormatDateValue(dt.Second, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case (byte)'f':
                FormatFractionalSeconds(dt.Millisecond, presentation, ref sb);
                break;
            case (byte)'Z':
                FormatTimezoneOffset(dt.Offset, presentation, ref sb);
                break;
            case (byte)'z':
                FormatTimezoneGmt(dt.Offset, ref sb);
                break;
            case (byte)'W':
                FormatDateValue(GetIsoWeekOfYear(dt), presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case (byte)'w':
                FormatDateValue(GetWeekOfMonth(dt), presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case (byte)'x':
                FormatDateValueOrName(GetMonthOfWeek(dt), presentation, minWidth, maxWidth, hardMaxWidth, MonthNames, ref sb);
                break;
            case (byte)'X':
                FormatDateValue(GetIsoWeekYear(dt), presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
                break;
            case (byte)'C':
                sb.Append("ISO"u8);
                break;
            case (byte)'E':
                sb.Append("ISO"u8);
                break;
            default:
                throw new JsonataException("D3132", SR.Format(SR.D3132_UnknownComponentSpecifier, (char)comp), 0);
        }
    }

    private static void FormatDateValue(int value, ReadOnlySpan<byte> presentation, int minWidth, int maxWidth, bool hardMaxWidth, ref Utf8ValueStringBuilder sb)
    {
        bool isOrdinal = false;
        ReadOnlySpan<byte> pres = presentation;

        // Check for trailing 'o' ordinal modifier in presentation
        if (pres.Length > 0 && pres[pres.Length - 1] == (byte)'o')
        {
            isOrdinal = true;
            pres = pres.Slice(0, pres.Length - 1);
        }

        if (pres.Length == 0)
        {
            pres = "1"u8;
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

    private static void FormatDateValueOrName(int value, ReadOnlySpan<byte> presentation, int minWidth, int maxWidth, bool hardMaxWidth, byte[][] names, ref Utf8ValueStringBuilder sb)
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

        if (presentation.SequenceEqual("N"u8))
        {
            isName = true;
            isUpper = true;
        }
        else if (presentation.SequenceEqual("n"u8))
        {
            isName = true;
            isLower = true;
        }
        else if (presentation.SequenceEqual("Nn"u8))
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

            byte[] name = names[idx];
            int nameLen = maxWidth >= 0 && name.Length > maxWidth ? maxWidth : name.Length;

            if (isUpper)
            {
                Span<byte> dest = sb.AppendSpan(nameLen);
                for (int j = 0; j < nameLen; j++)
                {
                    byte b = name[j];
                    dest[j] = b >= (byte)'a' && b <= (byte)'z' ? (byte)(b & 0xDF) : b;
                }
            }
            else if (isLower)
            {
                Span<byte> dest = sb.AppendSpan(nameLen);
                for (int j = 0; j < nameLen; j++)
                {
                    byte b = name[j];
                    dest[j] = b >= (byte)'A' && b <= (byte)'Z' ? (byte)(b | 0x20) : b;
                }
            }
            else
            {
                // Title case — already title case in the names array
                sb.Append(name.AsSpan(0, nameLen));
            }

            return;
        }

        FormatDateValue(value, presentation, minWidth, maxWidth, hardMaxWidth, ref sb);
    }

    private static void FormatFractionalSeconds(int milliseconds, ReadOnlySpan<byte> presentation, ref Utf8ValueStringBuilder sb)
    {
        // Count digit characters in presentation for number of fractional digits
        int digits = 0;
        for (int i = 0; i < presentation.Length; i++)
        {
            byte b = presentation[i];
            if (b >= (byte)'0' && b <= (byte)'9')
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

    private static void FormatTimezoneOffset(TimeSpan offset, ReadOnlySpan<byte> presentation, ref Utf8ValueStringBuilder sb)
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
        ReadOnlySpan<byte> p = presentation;
        if (p.Length > 0 && p[p.Length - 1] == (byte)'t')
        {
            useZForUtc = true;
            p = p.Slice(0, p.Length - 1);
        }

        if (useZForUtc && offset == TimeSpan.Zero)
        {
            sb.Append((byte)'Z');
            return;
        }

        // Detect pattern
        if (p.IndexOf((byte)':') >= 0)
        {
            // Colon-separated
            useColon = true;
            digits = 2; // zero-padded
        }
        else if (p.Length == 1 && p[0] == (byte)'0')
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
    private static bool IsValidWidthModifier(ReadOnlySpan<byte> spec)
    {
        if (spec.Length == 0)
        {
            return false;
        }

        // Split on '-' for range
        int dash = spec.IndexOf((byte)'-');
        if (dash >= 0)
        {
            ReadOnlySpan<byte> left = TrimAsciiWhitespace(spec.Slice(0, dash));
            ReadOnlySpan<byte> right = TrimAsciiWhitespace(spec.Slice(dash + 1));
            return IsWidthPart(left) && IsWidthPart(right);
        }

        return IsWidthPart(TrimAsciiWhitespace(spec));
    }

    private static bool IsWidthPart(ReadOnlySpan<byte> part)
    {
        if (part.Length == 1 && part[0] == (byte)'*')
        {
            return true;
        }

        if (part.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < part.Length; i++)
        {
            if (part[i] < (byte)'0' || part[i] > (byte)'9')
            {
                return false;
            }
        }

        return true;
    }

    private static int CountMandatoryDigits(ReadOnlySpan<byte> presentation)
    {
        // Count digit positions (0-9) and optional-digit markers (#) — these
        // define how many digit characters the presentation pattern requires.
        int count = 0;
        int i = 0;
        while (i < presentation.Length)
        {
            byte b = presentation[i];
            if (b < 0x80)
            {
                if ((b >= (byte)'0' && b <= (byte)'9') || b == (byte)'#')
                {
                    count++;
                }

                i++;
            }
            else
            {
                if (TryDecodeUtf8CodePoint(presentation.Slice(i), out int cp, out int consumed))
                {
                    if (GetUnicodeDecimalGroup((char)cp) >= 0)
                    {
                        count++;
                    }

                    i += consumed;
                }
                else
                {
                    i++;
                }
            }
        }

        return count;
    }

    private static void ParseWidthModifier(ReadOnlySpan<byte> spec, out int minWidth, out int maxWidth)
    {
        minWidth = -1;
        maxWidth = -1;

        int dash = spec.IndexOf((byte)'-');
        if (dash >= 0)
        {
            ReadOnlySpan<byte> minPart = TrimAsciiWhitespace(spec.Slice(0, dash));
            ReadOnlySpan<byte> maxPart = TrimAsciiWhitespace(spec.Slice(dash + 1));

            if (minPart.Length == 1 && minPart[0] == (byte)'*')
            {
                minWidth = -1;
            }
            else if (minPart.Length > 0 && Utf8Parser.TryParse(minPart, out int mn, out int mnConsumed) && mnConsumed == minPart.Length)
            {
                minWidth = mn;
            }

            if (maxPart.Length == 1 && maxPart[0] == (byte)'*')
            {
                maxWidth = -1;
            }
            else if (maxPart.Length > 0 && Utf8Parser.TryParse(maxPart, out int mx, out int mxConsumed) && mxConsumed == maxPart.Length)
            {
                maxWidth = mx;
            }
        }
        else
        {
            // Single value = both minimum and maximum width
            ReadOnlySpan<byte> trimmed = TrimAsciiWhitespace(spec);
            if (trimmed.Length == 1 && trimmed[0] == (byte)'*')
            {
                minWidth = -1;
            }
            else if (Utf8Parser.TryParse(trimmed, out int w, out int wConsumed) && wConsumed == trimmed.Length)
            {
                minWidth = w;
                maxWidth = w;
            }
        }
    }

    private static bool IsDecimalDigitPattern(ReadOnlySpan<byte> presentation)
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

        int i = 0;
        while (i < presentation.Length)
        {
            byte b = presentation[i];
            if (b < 0x80)
            {
                if (b == (byte)'#')
                {
                    hasPosition = true;
                    i++;
                    continue;
                }

                if (b >= (byte)'0' && b <= (byte)'9')
                {
                    if (detectedDecimalGroup.HasValue && detectedDecimalGroup.Value != 0)
                    {
                        throw new JsonataException("D3131", SR.D3131_TheFormatPictureStringContainsMixedDecimalDigitGroups, 0);
                    }

                    detectedDecimalGroup = 0;
                    hasDigit = true;
                    hasPosition = true;
                    i++;
                    continue;
                }

                // Must be a grouping separator if there's already been a digit or #
                if (!hasPosition)
                {
                    return false;
                }

                i++;
            }
            else
            {
                // Multi-byte: decode code point
                if (TryDecodeUtf8CodePoint(presentation.Slice(i), out int cp, out int consumed))
                {
                    int unicodeGroup = GetUnicodeDecimalGroup((char)cp);
                    if (unicodeGroup >= 0)
                    {
                        if (detectedDecimalGroup.HasValue && detectedDecimalGroup.Value != unicodeGroup)
                        {
                            throw new JsonataException("D3131", SR.D3131_TheFormatPictureStringContainsMixedDecimalDigitGroups, 0);
                        }

                        detectedDecimalGroup = unicodeGroup;
                        hasDigit = true;
                        hasPosition = true;
                        i += consumed;
                        continue;
                    }

                    // Grouping separator
                    if (!hasPosition)
                    {
                        return false;
                    }

                    i += consumed;
                }
                else
                {
                    if (!hasPosition)
                    {
                        return false;
                    }

                    i++;
                }
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

    private static void FormatDecimalDigit(long value, ReadOnlySpan<byte> presentation, bool isOrdinal, ref Utf8ValueStringBuilder result)
    {
        bool isNegative = value < 0;
        long absValue = Math.Abs(value);

        // Detect the digit base (ASCII or Unicode)
        int unicodeBase = 0;
        int pi = 0;
        while (pi < presentation.Length)
        {
            byte b = presentation[pi];
            if (b < 0x80)
            {
                if (b >= (byte)'0' && b <= (byte)'9')
                {
                    unicodeBase = 0;
                    break;
                }

                pi++;
            }
            else
            {
                if (TryDecodeUtf8CodePoint(presentation.Slice(pi), out int cp, out int consumed))
                {
                    int ug = GetUnicodeDecimalGroup((char)cp);
                    if (ug >= 0)
                    {
                        unicodeBase = ug;
                        break;
                    }

                    pi += consumed;
                }
                else
                {
                    pi++;
                }
            }
        }

        // Count mandatory digits (0s in pattern), find grouping separators
        int mandatoryDigits = 0;

        // Max 20 group separators in a pattern is more than enough
        Span<(int Position, char Separator)> groupSeparators = stackalloc (int, char)[20];
        int groupSepCount = 0;
        int digitCount = 0;

        // Process pattern from right to left for grouping
        // Iterate forward with Rune decoding to collect (position, char) pairs, then process reversed
        Span<(int CodePoint, int ByteOffset)> elements = stackalloc (int, int)[presentation.Length];
        int elemCount = 0;
        int ei = 0;
        while (ei < presentation.Length)
        {
            byte eb = presentation[ei];
            if (eb < 0x80)
            {
                elements[elemCount++] = (eb, ei);
                ei++;
            }
            else if (TryDecodeUtf8CodePoint(presentation.Slice(ei), out int ecp, out int econsumed))
            {
                elements[elemCount++] = (ecp, ei);
                ei += econsumed;
            }
            else
            {
                elements[elemCount++] = (eb, ei);
                ei++;
            }
        }

        for (int i = elemCount - 1; i >= 0; i--)
        {
            int cp = elements[i].CodePoint;
            char c = (char)cp;
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
                        result.AppendChar((char)(unicodeBase + (b - '0')));
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
                    result.AppendChar((char)(unicodeBase + (digits[i] - '0')));
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
                    result.AppendChar(sepChar);
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
                    result.AppendChar(sorted[sepIdx].Separator);
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

    private static readonly (byte[] Symbol, int Value)[] RomanValues =
    {
        ("M"u8.ToArray(), 1000), ("CM"u8.ToArray(), 900), ("D"u8.ToArray(), 500), ("CD"u8.ToArray(), 400),
        ("C"u8.ToArray(), 100), ("XC"u8.ToArray(), 90), ("L"u8.ToArray(), 50), ("XL"u8.ToArray(), 40),
        ("X"u8.ToArray(), 10), ("IX"u8.ToArray(), 9), ("V"u8.ToArray(), 5), ("IV"u8.ToArray(), 4), ("I"u8.ToArray(), 1),
    };

    private static long FromRomanNumerals(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length == 0)
        {
            return 0;
        }

        long result = 0;
        int i = 0;

        while (i < utf8.Length)
        {
            if (i + 1 < utf8.Length)
            {
                byte c0 = (byte)(utf8[i] & 0xDF);
                byte c1 = (byte)(utf8[i + 1] & 0xDF);
                bool found = false;
                foreach (var (symbol, val) in RomanValues)
                {
                    if (symbol.Length == 2 && symbol[0] == c0 && symbol[1] == c1)
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

            {
                byte c0 = (byte)(utf8[i] & 0xDF);
                bool foundOne = false;
                foreach (var (symbol, val) in RomanValues)
                {
                    if (symbol.Length == 1 && symbol[0] == c0)
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

    private static long FromAlpha(ReadOnlySpan<byte> utf8, bool uppercase)
    {
        if (utf8.Length == 0)
        {
            return 0;
        }

        long result = 0;

        foreach (byte b in utf8)
        {
            result = (result * 26) + ((byte)(b & 0xDF) - (byte)'A') + 1;
        }

        return result;
    }

    /// <summary>
    /// Appends a number formatted as English words to the builder, with optional
    /// ordinal form and casing.
    /// </summary>
    private static void AppendAsWords(long value, ReadOnlySpan<byte> casing, bool isOrdinal, ref Utf8ValueStringBuilder sb)
    {
        if (value == 0)
        {
            int start = sb.Length;
            sb.Append(isOrdinal ? "zeroth"u8 : "zero"u8);
            ApplyWordCasingInPlace(ref sb, start, casing);
            return;
        }

        int wordsStart = sb.Length;

        if (value < 0)
        {
            sb.Append("minus "u8);

            // Use ulong to handle long.MinValue safely
            ulong magnitude = value == long.MinValue
                ? ((ulong)long.MaxValue) + 1
                : (ulong)(-value);
            AppendNumberWords(magnitude, ref sb);
        }
        else
        {
            AppendNumberWords((ulong)value, ref sb);
        }

        if (isOrdinal)
        {
            OrdinalizeLastWord(ref sb, wordsStart);
        }

        ApplyWordCasingInPlace(ref sb, wordsStart, casing);
    }

    /// <summary>
    /// Appends a large number (as double, outside long range) formatted as English words.
    /// </summary>
    private static void AppendAsWordsLarge(double value, ReadOnlySpan<byte> casing, bool isOrdinal, ref Utf8ValueStringBuilder sb)
    {
        int wordsStart = sb.Length;

        bool isNegative = value < 0;
        double absValue = Math.Abs(value);

        if (isNegative)
        {
            sb.Append("minus "u8);
        }

        AppendNumberWordsLarge(absValue, ref sb);

        if (isOrdinal)
        {
            OrdinalizeLastWord(ref sb, wordsStart);
        }

        ApplyWordCasingInPlace(ref sb, wordsStart, casing);
    }

    /// <summary>
    /// Appends the cardinal English word representation of a non-negative integer
    /// directly to a <see cref="Utf8ValueStringBuilder"/>.
    /// </summary>
    private static void AppendNumberWords(ulong value, ref Utf8ValueStringBuilder sb)
    {
        if (value == 0)
        {
            sb.Append("zero"u8);
            return;
        }

        // Handle very large numbers by chaining "trillion"
        if (value >= 1_000_000_000_000_000UL)
        {
            ulong trillions = value / 1_000_000_000_000UL;
            ulong remainder = value % 1_000_000_000_000UL;

            AppendNumberWords(trillions, ref sb);
            sb.Append(" trillion"u8);
            if (remainder == 0)
            {
                return;
            }

            sb.Append(remainder < 100 ? " and "u8 : ", "u8);
            AppendNumberWords(remainder, ref sb);
            return;
        }

        bool needsSeparator = false;

        foreach (var (name, scale) in ScaleWords)
        {
            if ((ulong)scale > 1_000_000_000_000UL)
            {
                continue; // Skip trillion, handled above
            }

            if (value >= (ulong)scale)
            {
                ulong count = value / (ulong)scale;
                value %= (ulong)scale;

                if (needsSeparator)
                {
                    sb.Append(", "u8);
                }

                if ((ulong)scale >= 1000)
                {
                    AppendNumberWords(count, ref sb);
                    sb.Append((byte)' ');
                }
                else
                {
                    // hundred
                    sb.Append(Ones[count]);
                    sb.Append((byte)' ');
                }

                sb.Append(name);
                needsSeparator = true;
            }
        }

        if (value > 0)
        {
            if (needsSeparator)
            {
                sb.Append(" and "u8);
            }

            if (value < 20)
            {
                sb.Append(Ones[value]);
            }
            else
            {
                sb.Append(Tens[value / 10]);
                ulong ones = value % 10;
                if (ones > 0)
                {
                    sb.Append((byte)'-');
                    sb.Append(Ones[ones]);
                }
            }
        }
    }

    /// <summary>
    /// Appends the cardinal English word representation of a large number (double)
    /// directly to a <see cref="Utf8ValueStringBuilder"/>.
    /// </summary>
    private static void AppendNumberWordsLarge(double value, ref Utf8ValueStringBuilder sb)
    {
        if (value < 1_000_000_000_000.0)
        {
            AppendNumberWords((ulong)value, ref sb);
            return;
        }

        double trillions = Math.Floor(value / 1_000_000_000_000.0);
        double remainder = value - (trillions * 1_000_000_000_000.0);

        if (trillions >= 1_000_000_000_000.0)
        {
            AppendNumberWordsLarge(trillions, ref sb);
        }
        else
        {
            AppendNumberWords((ulong)trillions, ref sb);
        }

        sb.Append(" trillion"u8);

        if (remainder < 1.0)
        {
            return;
        }

        sb.Append(remainder < 100 ? " and "u8 : ", "u8);
        AppendNumberWords((ulong)remainder, ref sb);
    }

    /// <summary>
    /// Finds the last word in the builder (after <paramref name="regionStart"/>),
    /// replaces it with its ordinal form.
    /// </summary>
    private static void OrdinalizeLastWord(ref Utf8ValueStringBuilder sb, int regionStart)
    {
        Span<byte> raw = sb.RawBytes;
        int end = sb.Length;

        // Scan backward to find last word boundary (space or hyphen)
        int tokenStart = regionStart;
        for (int i = end - 1; i >= regionStart; i--)
        {
            if (raw[i] == (byte)' ' || raw[i] == (byte)'-')
            {
                tokenStart = i + 1;
                break;
            }
        }

        int tokenLength = end - tokenStart;
        ReadOnlySpan<byte> tokenBytes = raw.Slice(tokenStart, tokenLength);

        // Try OrdinalWordMap lookup (all entries are ASCII, case-insensitive)
        foreach (var (key, value) in OrdinalWordMap)
        {
            if (Utf8EqualsAsciiIgnoreCase(tokenBytes, key))
            {
                sb.Length = tokenStart;
                sb.Append(value);
                return;
            }
        }

        // Check if it ends with "y" → change to "ieth"
        if (tokenLength > 0 && tokenBytes[tokenLength - 1] == (byte)'y')
        {
            sb.Length = end - 1; // Remove the 'y'
            sb.Append("ieth"u8);
            return;
        }

        // Default: just append "th"
        sb.Append("th"u8);
    }

    /// <summary>
    /// Applies word casing (W = UPPER, w = lower, Ww = Title Case) in-place
    /// over the region <c>[start..sb.Length)</c> of the builder.
    /// </summary>
    private static void ApplyWordCasingInPlace(ref Utf8ValueStringBuilder sb, int start, ReadOnlySpan<byte> casing)
    {
        if (casing.SequenceEqual("w"u8))
        {
            // Already lowercase — nothing to do
            return;
        }

        Span<byte> raw = sb.RawBytes;
        int end = sb.Length;

        if (casing.SequenceEqual("W"u8))
        {
            // Convert all lowercase ASCII to uppercase
            for (int i = start; i < end; i++)
            {
                byte b = raw[i];
                if (b >= (byte)'a' && b <= (byte)'z')
                {
                    raw[i] = (byte)(b - 32);
                }
            }

            return;
        }

        if (casing.SequenceEqual("Ww"u8))
        {
            // Title case: capitalize first letter of each word, except "and"
            bool capitalizeNext = true;
            int i = start;
            while (i < end)
            {
                byte b = raw[i];
                if (b == (byte)' ' || b == (byte)'-' || b == (byte)',')
                {
                    capitalizeNext = true;
                    i++;
                }
                else if (capitalizeNext)
                {
                    // Check if this word is "and"
                    if (i + 3 < end &&
                        raw[i] == (byte)'a' && raw[i + 1] == (byte)'n' && raw[i + 2] == (byte)'d' &&
                        (raw[i + 3] == (byte)' ' || raw[i + 3] == (byte)'-' || raw[i + 3] == (byte)','))
                    {
                        i += 3;
                        capitalizeNext = false;
                    }
                    else
                    {
                        if (b >= (byte)'a' && b <= (byte)'z')
                        {
                            raw[i] = (byte)(b - 32);
                        }

                        capitalizeNext = false;
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }
        }
    }

    private static bool TryParseWordsToNumber(ReadOnlySpan<byte> utf8, bool isOrdinal, out long value)
    {
        value = 0;
        if (TryParseWordsToNumberDouble(utf8, isOrdinal, out double dblValue))
        {
            if (dblValue >= long.MinValue && dblValue <= long.MaxValue)
            {
                value = (long)dblValue;
            }

            return true;
        }

        return false;
    }

    private static bool TryParseWordsToNumberDouble(ReadOnlySpan<byte> utf8, bool isOrdinal, out double value)
    {
        value = 0;

        // Trim leading/trailing spaces
        int trimStart = 0;
        int trimEnd = utf8.Length;
        while (trimStart < trimEnd && utf8[trimStart] == (byte)' ')
        {
            trimStart++;
        }

        while (trimEnd > trimStart && utf8[trimEnd - 1] == (byte)' ')
        {
            trimEnd--;
        }

        ReadOnlySpan<byte> text = utf8.Slice(trimStart, trimEnd - trimStart);

        if (text.Length == 0)
        {
            return false;
        }

        if (Utf8EqualsAsciiIgnoreCase(text, "zero"u8) || Utf8EqualsAsciiIgnoreCase(text, "zeroth"u8))
        {
            value = 0;
            return true;
        }

        // If ordinal, convert ordinal words back to cardinal using a stack-allocated buffer
        if (isOrdinal)
        {
            Span<byte> buffer = stackalloc byte[text.Length];
            int cardinalLen = ConvertOrdinalToCardinal(text, buffer);
            return TryParseCardinalWords(buffer.Slice(0, cardinalLen), out value);
        }

        return TryParseCardinalWords(text, out value);
    }

    /// <summary>
    /// Converts the last ordinal word in the text to its cardinal form,
    /// writing the result into <paramref name="buffer"/>.
    /// Returns the number of bytes written.
    /// </summary>
    private static int ConvertOrdinalToCardinal(ReadOnlySpan<byte> text, Span<byte> buffer)
    {
        // Find the last word boundary (space or hyphen, searching from end)
        int lastBoundary = -1;
        for (int i = text.Length - 1; i >= 0; i--)
        {
            if (text[i] == (byte)' ' || text[i] == (byte)'-')
            {
                lastBoundary = i;
                break;
            }
        }

        ReadOnlySpan<byte> lastWord = lastBoundary >= 0 ? text.Slice(lastBoundary + 1) : text;
        int prefixLen = lastBoundary >= 0 ? lastBoundary + 1 : 0;

        // Try OrdinalWordMap reverse lookup (check if last word is an ordinal form)
        foreach (var (key, value) in OrdinalWordMap)
        {
            if (Utf8EqualsAsciiIgnoreCase(lastWord, value))
            {
                text.Slice(0, prefixLen).CopyTo(buffer);
                key.AsSpan().CopyTo(buffer.Slice(prefixLen));
                return prefixLen + key.Length;
            }
        }

        // Handle generic "th" suffix
        if (lastWord.Length > 2 && Utf8EndsWithAsciiIgnoreCase(lastWord, "th"u8))
        {
            text.Slice(0, text.Length - 2).CopyTo(buffer);
            return text.Length - 2;
        }

        // No conversion needed — copy original
        text.CopyTo(buffer);
        return text.Length;
    }

    /// <summary>
    /// Parses cardinal English words into a double value using UTF-8 byte tokenization.
    /// Handles commas and "and" as separators, hyphenated tokens, and scale words.
    /// </summary>
    private static bool TryParseCardinalWords(ReadOnlySpan<byte> text, out double value)
    {
        value = 0;
        bool hasTokens = false;

        double current = 0;
        double result = 0;
        int pos = 0;

        while (pos < text.Length)
        {
            // Skip whitespace and commas
            while (pos < text.Length && (text[pos] == (byte)' ' || text[pos] == (byte)','))
            {
                pos++;
            }

            if (pos >= text.Length)
            {
                break;
            }

            // Find end of token
            int tokenStart = pos;
            while (pos < text.Length && text[pos] != (byte)' ' && text[pos] != (byte)',')
            {
                pos++;
            }

            ReadOnlySpan<byte> token = text.Slice(tokenStart, pos - tokenStart);

            // Skip "and"
            if (Utf8EqualsAsciiIgnoreCase(token, "and"u8))
            {
                continue;
            }

            hasTokens = true;

            // Handle hyphenated tokens (e.g. "twenty-three")
            int hyphenIdx = -1;
            for (int i = 0; i < token.Length; i++)
            {
                if (token[i] == (byte)'-')
                {
                    hyphenIdx = i;
                    break;
                }
            }

            if (hyphenIdx > 0)
            {
                ReadOnlySpan<byte> left = token.Slice(0, hyphenIdx);
                ReadOnlySpan<byte> right = token.Slice(hyphenIdx + 1);
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

        if (!hasTokens)
        {
            return false;
        }

        value = result + current;
        return true;
    }

    private static long WordToNumber(ReadOnlySpan<byte> word)
    {
        for (int i = 0; i < Ones.Length; i++)
        {
            if (Utf8EqualsAsciiIgnoreCase(word, Ones[i]))
            {
                return i;
            }
        }

        for (int i = 2; i < Tens.Length; i++)
        {
            if (Utf8EqualsAsciiIgnoreCase(word, Tens[i]))
            {
                return i * 10;
            }
        }

        return -1;
    }

    private static long GetScaleValue(ReadOnlySpan<byte> word)
    {
        foreach (var (name, val) in ScaleWords)
        {
            if (Utf8EqualsAsciiIgnoreCase(word, name))
            {
                return val;
            }
        }

        return -1;
    }

    /// <summary>
    /// Case-insensitive ASCII comparison of two <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/>
    /// (UTF-8). Non-ASCII bytes (≥0x80) are compared exactly.
    /// </summary>
    private static bool Utf8EqualsAsciiIgnoreCase(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            byte ba = a[i];
            byte bb = b[i];
            if (ba == bb)
            {
                continue;
            }

            if ((ba | 0x20) >= (byte)'a' && (ba | 0x20) <= (byte)'z' &&
                (ba | 0x20) == (bb | 0x20))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Case-insensitive ASCII check for whether a <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/>
    /// (UTF-8) ends with a given pure-ASCII suffix.
    /// </summary>
    private static bool Utf8EndsWithAsciiIgnoreCase(ReadOnlySpan<byte> span, ReadOnlySpan<byte> suffix)
    {
        if (span.Length < suffix.Length)
        {
            return false;
        }

        return Utf8EqualsAsciiIgnoreCase(span.Slice(span.Length - suffix.Length), suffix);
    }

    private static bool TryParseDecimalDigit(ReadOnlySpan<byte> utf8, ReadOnlySpan<byte> presentation, bool isOrdinal, out long value)
    {
        value = 0;
        ReadOnlySpan<byte> text = utf8;

        // Strip ordinal suffix
        if (isOrdinal && text.Length >= 2)
        {
            if (Utf8EndsWithAsciiIgnoreCase(text, "st"u8) ||
                Utf8EndsWithAsciiIgnoreCase(text, "nd"u8) ||
                Utf8EndsWithAsciiIgnoreCase(text, "rd"u8) ||
                Utf8EndsWithAsciiIgnoreCase(text, "th"u8))
            {
                text = text.Slice(0, text.Length - 2);
            }
        }

        // Detect unicode digit base from presentation
        int unicodeBase = 0;
        {
            int pi = 0;
            while (pi < presentation.Length)
            {
                byte b = presentation[pi];
                if (b < 0x80)
                {
                    int ug = GetUnicodeDecimalGroup((char)b);
                    if (ug >= 0)
                    {
                        unicodeBase = ug;
                        break;
                    }

                    pi++;
                }
                else if (TryDecodeUtf8CodePoint(presentation.Slice(pi), out int cp, out int consumed))
                {
                    int ug = GetUnicodeDecimalGroup((char)cp);
                    if (ug >= 0)
                    {
                        unicodeBase = ug;
                        break;
                    }

                    pi += consumed;
                }
                else
                {
                    pi++;
                }
            }
        }

        // Collect grouping separator byte sequences from the presentation.
        // Since presentation is already UTF-8, we extract separator byte spans directly.
        Span<byte> groupSepBuf = stackalloc byte[32];
        Span<int> groupSepLengths = stackalloc int[8];
        int groupCount = 0;
        {
            int pi = 0;
            while (pi < presentation.Length)
            {
                byte b = presentation[pi];
                int cpLen;
                bool isDigitOrPlaceholder;

                if (b < 0x80)
                {
                    cpLen = 1;
                    isDigitOrPlaceholder = b == (byte)'0' || b == (byte)'#' || (b >= (byte)'1' && b <= (byte)'9');
                    if (!isDigitOrPlaceholder)
                    {
                        int ug = GetUnicodeDecimalGroup((char)b);
                        isDigitOrPlaceholder = ug >= 0;
                    }
                }
                else if (TryDecodeUtf8CodePoint(presentation.Slice(pi), out int cp2, out int consumed))
                {
                    cpLen = consumed;
                    isDigitOrPlaceholder = GetUnicodeDecimalGroup((char)cp2) >= 0;
                }
                else
                {
                    cpLen = 1;
                    isDigitOrPlaceholder = false;
                }

                if (!isDigitOrPlaceholder)
                {
                    // This is a grouping separator — extract its UTF-8 bytes
                    ReadOnlySpan<byte> sepBytes = presentation.Slice(pi, cpLen);
                    int sepOffset = groupCount * 4;

                    // Check for duplicates
                    bool found = false;
                    for (int j = 0; j < groupCount; j++)
                    {
                        int jOff = j * 4;
                        if (groupSepLengths[j] == cpLen &&
                            groupSepBuf.Slice(jOff, cpLen).SequenceEqual(sepBytes))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found && groupCount < 8)
                    {
                        sepBytes.CopyTo(groupSepBuf.Slice(sepOffset));
                        groupSepLengths[groupCount] = cpLen;
                        groupCount++;
                    }
                }

                pi += cpLen;
            }
        }

        // For unicode digits, decode UTF-8 code points and accumulate manually
        if (unicodeBase != 0)
        {
            bool negative = false;
            long accum = 0;
            bool hasDigits = false;
            int pos = 0;

            while (pos < text.Length)
            {
                byte b = text[pos];

                // ASCII byte
                if (b < 0x80)
                {
                    if (b == (byte)'-' && !hasDigits)
                    {
                        negative = true;
                        pos++;
                        continue;
                    }

                    // Check if it's an ASCII grouping separator
                    bool isSep = false;
                    for (int g = 0; g < groupCount; g++)
                    {
                        if (groupSepLengths[g] == 1 && groupSepBuf[g * 4] == b)
                        {
                            isSep = true;
                            break;
                        }
                    }

                    if (isSep)
                    {
                        pos++;
                        continue;
                    }

                    // ASCII digit
                    if (b >= (byte)'0' && b <= (byte)'9')
                    {
                        accum = (accum * 10) + (b - (byte)'0');
                        hasDigits = true;
                        pos++;
                        continue;
                    }

                    break;
                }

                // Multi-byte UTF-8: decode code point
                int cp;
                int seqLen;
                if ((b & 0xE0) == 0xC0 && pos + 1 < text.Length)
                {
                    cp = ((b & 0x1F) << 6) | (text[pos + 1] & 0x3F);
                    seqLen = 2;
                }
                else if ((b & 0xF0) == 0xE0 && pos + 2 < text.Length)
                {
                    cp = ((b & 0x0F) << 12) | ((text[pos + 1] & 0x3F) << 6) | (text[pos + 2] & 0x3F);
                    seqLen = 3;
                }
                else if ((b & 0xF8) == 0xF0 && pos + 3 < text.Length)
                {
                    cp = ((b & 0x07) << 18) | ((text[pos + 1] & 0x3F) << 12) | ((text[pos + 2] & 0x3F) << 6) | (text[pos + 3] & 0x3F);
                    seqLen = 4;
                }
                else
                {
                    break;
                }

                // Check multi-byte grouping separator
                bool isMultiSep = false;
                for (int g = 0; g < groupCount; g++)
                {
                    int gOff = g * 4;
                    int gLen = groupSepLengths[g];
                    if (gLen == seqLen && text.Slice(pos, seqLen).SequenceEqual(groupSepBuf.Slice(gOff, gLen)))
                    {
                        isMultiSep = true;
                        break;
                    }
                }

                if (isMultiSep)
                {
                    pos += seqLen;
                    continue;
                }

                // Unicode digit
                if (cp >= unicodeBase && cp <= unicodeBase + 9)
                {
                    accum = (accum * 10) + (cp - unicodeBase);
                    hasDigits = true;
                    pos += seqLen;
                    continue;
                }

                break;
            }

            if (!hasDigits)
            {
                return false;
            }

            value = negative ? -accum : accum;
            return true;
        }

        // ASCII-only fast path: strip grouping separators and parse
        bool needsStrip = groupCount > 0;
        if (needsStrip)
        {
            Span<byte> buf = stackalloc byte[text.Length];
            int bufLen = 0;

            for (int i = 0; i < text.Length; i++)
            {
                byte b = text[i];
                bool isSep = false;
                for (int g = 0; g < groupCount; g++)
                {
                    if (groupSepLengths[g] == 1 && groupSepBuf[g * 4] == b)
                    {
                        isSep = true;
                        break;
                    }
                }

                if (!isSep)
                {
                    buf[bufLen++] = b;
                }
            }

            return TryParseAsciiLong(buf.Slice(0, bufLen), out value);
        }

        return TryParseAsciiLong(text, out value);
    }

    /// <summary>
    /// Parses an ASCII integer from a UTF-8 byte span. Handles optional leading minus sign.
    /// </summary>
    private static bool TryParseAsciiLong(ReadOnlySpan<byte> utf8, out long value)
    {
        value = 0;
        if (utf8.Length == 0)
        {
            return false;
        }

        int pos = 0;
        bool negative = false;
        if (utf8[0] == (byte)'-')
        {
            negative = true;
            pos++;
        }
        else if (utf8[0] == (byte)'+')
        {
            pos++;
        }

        if (pos >= utf8.Length)
        {
            return false;
        }

        bool hasDigits = false;
        while (pos < utf8.Length)
        {
            byte b = utf8[pos];
            if (b < (byte)'0' || b > (byte)'9')
            {
                break;
            }

            value = (value * 10) + (b - (byte)'0');
            hasDigits = true;
            pos++;
        }

        if (!hasDigits)
        {
            return false;
        }

        if (negative)
        {
            value = -value;
        }

        return true;
    }

    private struct PictureComponent
    {
        public bool IsLiteral;
        public byte[]? Literal;
        public byte Component;
        public byte[] Presentation;
        public int DigitWidth;
    }

    private static List<PictureComponent> ParsePictureString(ReadOnlySpan<byte> picture)
    {
        var components = new List<PictureComponent>();
        int i = 0;
        Span<byte> stripBuf = stackalloc byte[64];

        while (i < picture.Length)
        {
            byte b = picture[i];
            if (b == (byte)'[')
            {
                if (i + 1 < picture.Length && picture[i + 1] == (byte)'[')
                {
                    components.Add(new PictureComponent { IsLiteral = true, Literal = "["u8.ToArray() });
                    i += 2;
                    continue;
                }

                int end = -1;
                for (int j = i + 1; j < picture.Length; j++)
                {
                    if (picture[j] == (byte)']')
                    {
                        end = j;
                        break;
                    }
                }

                if (end < 0)
                {
                    throw new JsonataException("D3135", SR.D3135_PictureStringContainsAWithNoMatching, 0);
                }

                // Extract marker bytes and strip ALL ASCII whitespace
                ReadOnlySpan<byte> marker = picture.Slice(i + 1, end - i - 1);
                int strippedLen = StripAllAsciiWhitespace(marker, stripBuf);
                ReadOnlySpan<byte> stripped = stripBuf.Slice(0, strippedLen);

                if (strippedLen == 0)
                {
                    i = end + 1;
                    continue;
                }

                byte comp = stripped[0];
                ReadOnlySpan<byte> pres = strippedLen > 1 ? stripped.Slice(1, strippedLen - 1) : ReadOnlySpan<byte>.Empty;

                // Remove width modifier using last-comma approach
                int lastComma = -1;
                for (int ci = pres.Length - 1; ci >= 0; ci--)
                {
                    if (pres[ci] == (byte)',')
                    {
                        ReadOnlySpan<byte> candidate = pres.Slice(ci + 1);
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
                    ReadOnlySpan<byte> widthPart = pres.Slice(lastComma + 1);
                    pres = pres.Slice(0, lastComma);
                    ParseWidthModifier(widthPart, out _, out maxWidthFromPic);
                }

                // Count digit positions in presentation for fixed-width parsing
                for (int di = 0; di < pres.Length; di++)
                {
                    byte dc = pres[di];
                    if ((dc >= (byte)'0' && dc <= (byte)'9') || dc == (byte)'#')
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
                if (comp == (byte)'Y' || comp == (byte)'M' || comp == (byte)'D' || comp == (byte)'d' ||
                    comp == (byte)'H' || comp == (byte)'h' || comp == (byte)'m' || comp == (byte)'s' ||
                    comp == (byte)'f' || comp == (byte)'P' || comp == (byte)'Z' || comp == (byte)'z' ||
                    comp == (byte)'F' || comp == (byte)'C' || comp == (byte)'E' || comp == (byte)'W' ||
                    comp == (byte)'w' || comp == (byte)'X' || comp == (byte)'x')
                {
                    // Check for name presentation which is not supported for Y
                    if (comp == (byte)'Y' && (pres.SequenceEqual("N"u8) || pres.SequenceEqual("Nn"u8)))
                    {
                        throw new JsonataException("D3133", SR.D3133_TheYearComponentCannotBeRepresentedAsAName, 0);
                    }

                    components.Add(new PictureComponent
                    {
                        IsLiteral = false,
                        Component = comp,
                        Presentation = pres.ToArray(),
                        DigitWidth = digitWidth,
                    });
                }
                else
                {
                    throw new JsonataException("D3132", SR.Format(SR.D3132_UnknownComponentSpecifier, (char)comp), 0);
                }

                i = end + 1;
            }
            else if (b == (byte)']')
            {
                if (i + 1 < picture.Length && picture[i + 1] == (byte)']')
                {
                    components.Add(new PictureComponent { IsLiteral = true, Literal = "]"u8.ToArray() });
                    i += 2;
                }
                else
                {
                    components.Add(new PictureComponent { IsLiteral = true, Literal = "]"u8.ToArray() });
                    i++;
                }
            }
            else
            {
                // Collect literal bytes (may be multi-byte UTF-8)
                int start = i;
                while (i < picture.Length && picture[i] != (byte)'[' && picture[i] != (byte)']')
                {
                    i++;
                }

                components.Add(new PictureComponent { IsLiteral = true, Literal = picture.Slice(start, i - start).ToArray() });
            }
        }

        return components;
    }

    private static int ParseIntegerValue(ReadOnlySpan<byte> utf8, ref int pos, ReadOnlySpan<byte> presentation, int overrideMaxDigits = 0)
    {
        if (pos >= utf8.Length)
        {
            return -1;
        }

        // Check for 'N' or 'Nn' name presentation (used for year names - not supported)
        if (presentation.SequenceEqual("N"u8) || presentation.SequenceEqual("Nn"u8))
        {
            throw new JsonataException("D3133", SR.D3133_TheComponentCannotBeRepresentedAsAName, 0);
        }

        // Check for Roman numerals
        if (presentation.SequenceEqual("I"u8) || presentation.SequenceEqual("i"u8))
        {
            return ParseRoman(utf8, ref pos);
        }

        // Check for words
        if (presentation.SequenceEqual("w"u8) || presentation.SequenceEqual("W"u8) || presentation.SequenceEqual("Ww"u8))
        {
            return ParseWords(utf8, ref pos, false);
        }

        // Check for ordinal words
        if (presentation.SequenceEqual("wo"u8) || presentation.SequenceEqual("Wo"u8) || presentation.SequenceEqual("Wwo"u8))
        {
            return ParseWords(utf8, ref pos, true);
        }

        // Check for alpha
        if (presentation.SequenceEqual("a"u8))
        {
            return ParseAlpha(utf8, ref pos, false);
        }

        if (presentation.SequenceEqual("A"u8))
        {
            return ParseAlpha(utf8, ref pos, true);
        }

        // Check for ordinal suffix
        bool isOrdinal = false;
        ReadOnlySpan<byte> pres = presentation;
        if (pres.Length > 0 && pres[pres.Length - 1] == (byte)'o')
        {
            isOrdinal = true;
            pres = pres.Slice(0, pres.Length - 1);
        }

        // Count expected digits from presentation (excluding '#')
        int maxDigits = 0;
        for (int i = 0; i < pres.Length; i++)
        {
            byte b = pres[i];
            if (b >= (byte)'0' && b <= (byte)'9')
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
        if (pos < utf8.Length && utf8[pos] == (byte)'-')
        {
            negative = true;
            pos++;
        }

        int digitsRead = 0;
        int result = 0;
        while (pos < utf8.Length && utf8[pos] >= (byte)'0' && utf8[pos] <= (byte)'9')
        {
            result = (result * 10) + (utf8[pos] - (byte)'0');
            pos++;
            digitsRead++;
            if (fixedWidth && digitsRead >= maxDigits)
            {
                break;
            }
        }

        if (digitsRead == 0)
        {
            pos = start;
            return -1;
        }

        if (negative)
        {
            result = -result;
        }

        // Skip ordinal suffix if present
        if (isOrdinal && pos + 1 < utf8.Length)
        {
            ReadOnlySpan<byte> suffix = utf8.Slice(pos, 2);
            if (Utf8EqualsAsciiIgnoreCase(suffix, "st"u8) || Utf8EqualsAsciiIgnoreCase(suffix, "nd"u8) ||
                Utf8EqualsAsciiIgnoreCase(suffix, "rd"u8) || Utf8EqualsAsciiIgnoreCase(suffix, "th"u8))
            {
                pos += 2;
            }
        }

        return result;
    }

    private static int ParseDateComponent(ReadOnlySpan<byte> utf8, ref int pos, ReadOnlySpan<byte> presentation, byte[][] names, int overrideMaxDigits = 0)
    {
        if (pos >= utf8.Length)
        {
            return -1;
        }

        // Check for name-based parsing (N, n, Nn, or starts with "Nn")
        if (presentation.SequenceEqual("N"u8) || presentation.SequenceEqual("n"u8) || presentation.SequenceEqual("Nn"u8) ||
            (presentation.Length >= 2 && presentation[0] == (byte)'N' && presentation[1] == (byte)'n'))
        {
            return ParseName(utf8, ref pos, names);
        }

        // Check for Roman numerals
        if (presentation.SequenceEqual("I"u8) || presentation.SequenceEqual("i"u8))
        {
            return ParseRoman(utf8, ref pos);
        }

        // Check for alpha
        if (presentation.SequenceEqual("A"u8))
        {
            return ParseAlpha(utf8, ref pos, true);
        }

        if (presentation.SequenceEqual("a"u8))
        {
            return ParseAlpha(utf8, ref pos, false);
        }

        // Numeric
        return ParseIntegerValue(utf8, ref pos, presentation, overrideMaxDigits);
    }

    private static int ParseName(ReadOnlySpan<byte> utf8, ref int pos, byte[][] names)
    {
        ReadOnlySpan<byte> remaining = utf8.Slice(pos);

        // Try to match from longest to shortest
        int bestMatch = -1;
        int bestLength = 0;

        for (int i = 0; i < names.Length; i++)
        {
            byte[] name = names[i];

            // Try full name
            if (remaining.Length >= name.Length &&
                Utf8EqualsAsciiIgnoreCase(remaining.Slice(0, name.Length), name))
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
                if (remaining.Length >= len)
                {
                    if (Utf8EqualsAsciiIgnoreCase(remaining.Slice(0, len), name.AsSpan(0, len)) && len > bestLength)
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

    private static int ParseRoman(ReadOnlySpan<byte> utf8, ref int pos)
    {
        int start = pos;

        // Collect roman numeral characters
        while (pos < utf8.Length)
        {
            byte c = (byte)(utf8[pos] & 0xDF); // uppercase
            if (c == (byte)'M' || c == (byte)'D' || c == (byte)'C' || c == (byte)'L' ||
                c == (byte)'X' || c == (byte)'V' || c == (byte)'I')
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

        return (int)FromRomanNumerals(utf8.Slice(start, pos - start));
    }

    private static int ParseWords(ReadOnlySpan<byte> utf8, ref int pos, bool isOrdinal)
    {
        int start = pos;

        // Greedily match word characters (ASCII letters, hyphens, spaces, commas)
        int end = pos;
        while (end < utf8.Length)
        {
            byte b = utf8[end];
            if ((b | 0x20) >= (byte)'a' && (b | 0x20) <= (byte)'z')
            {
                end++;
            }
            else if (b == (byte)'-' || b == (byte)' ' || b == (byte)',')
            {
                end++;
            }
            else
            {
                break;
            }
        }

        // Trim trailing separators
        while (end > start && (utf8[end - 1] == (byte)' ' || utf8[end - 1] == (byte)','))
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
            ReadOnlySpan<byte> wordSpan = utf8.Slice(start, tryEnd - start);

            // Try cardinal first, then ordinal (or vice versa based on isOrdinal)
            if (TryParseWordsToNumber(wordSpan, isOrdinal, out long value) ||
                TryParseWordsToNumber(wordSpan, !isOrdinal, out value))
            {
                pos = tryEnd;
                return (int)value;
            }

            // Back off: find the last space before tryEnd
            int lastSpace = -1;
            for (int si = tryEnd - 1; si > start; si--)
            {
                if (utf8[si] == (byte)' ')
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
            while (tryEnd > start && (utf8[tryEnd - 1] == (byte)' ' || utf8[tryEnd - 1] == (byte)','))
            {
                tryEnd--;
            }
        }

        return -1;
    }

    private static int ParseAlpha(ReadOnlySpan<byte> utf8, ref int pos, bool uppercase)
    {
        int start = pos;
        while (pos < utf8.Length)
        {
            byte b = utf8[pos];
            if ((b | 0x20) >= (byte)'a' && (b | 0x20) <= (byte)'z')
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

        return (int)FromAlpha(utf8.Slice(start, pos - start), uppercase);
    }

    private static int ParseFractionalSeconds(ReadOnlySpan<byte> utf8, ref int pos)
    {
        int start = pos;
        while (pos < utf8.Length && utf8[pos] >= (byte)'0' && utf8[pos] <= (byte)'9')
        {
            pos++;
        }

        if (pos == start)
        {
            return -1;
        }

        int digitCount = pos - start;

        // Accumulate first 3 digits (milliseconds)
        int result = 0;
        int take = digitCount > 3 ? 3 : digitCount;
        for (int i = 0; i < take; i++)
        {
            result = (result * 10) + (utf8[start + i] - (byte)'0');
        }

        // Pad if fewer than 3 digits
        for (int i = take; i < 3; i++)
        {
            result *= 10;
        }

        return result;
    }

    /// <summary>
    /// Parses AM/PM from UTF-8 bytes. Returns 0 if not found, 1 for AM, 2 for PM.
    /// </summary>
    private static int ParseAmPm(ReadOnlySpan<byte> utf8, ref int pos)
    {
        if (pos + 2 <= utf8.Length)
        {
            ReadOnlySpan<byte> twoBytes = utf8.Slice(pos, 2);
            if (Utf8EqualsAsciiIgnoreCase(twoBytes, "am"u8))
            {
                pos += 2;
                return 1;
            }

            if (Utf8EqualsAsciiIgnoreCase(twoBytes, "pm"u8))
            {
                pos += 2;
                return 2;
            }
        }

        return 0;
    }

    private static int? ParseTimezoneOffset(ReadOnlySpan<byte> utf8, ref int pos)
    {
        if (pos >= utf8.Length)
        {
            return null;
        }

        // "Z" for UTC
        if (utf8[pos] == (byte)'Z')
        {
            pos++;
            return 0;
        }

        // +HH:MM or +HHMM or +HH
        if (utf8[pos] != (byte)'+' && utf8[pos] != (byte)'-')
        {
            return null;
        }

        byte sign = utf8[pos];
        pos++;

        // Read hour digits
        int hStart = pos;
        int hourVal = 0;
        while (pos < utf8.Length && utf8[pos] >= (byte)'0' && utf8[pos] <= (byte)'9')
        {
            hourVal = (hourVal * 10) + (utf8[pos] - (byte)'0');
            pos++;
        }

        int hLen = pos - hStart;
        if (hLen == 0)
        {
            return null;
        }

        int minutes = 0;
        if (pos < utf8.Length && utf8[pos] == (byte)':')
        {
            pos++; // skip colon
            int mStart = pos;
            while (pos < utf8.Length && utf8[pos] >= (byte)'0' && utf8[pos] <= (byte)'9')
            {
                minutes = (minutes * 10) + (utf8[pos] - (byte)'0');
                pos++;
            }

            if (pos == mStart)
            {
                minutes = 0;
            }
        }
        else if (hLen == 4)
        {
            // HHMM format: split hourVal into HH and MM
            minutes = hourVal % 100;
            hourVal /= 100;
        }

        int totalMinutes = (hourVal * 60) + minutes;
        return sign == (byte)'-' ? -totalMinutes : totalMinutes;
    }

    private static int? ParseTimezoneNameOffset(ReadOnlySpan<byte> utf8, ref int pos)
    {
        // Format: "GMT+HH:MM" or "GMT-HH:MM" or just "GMT"
        if (pos + 3 <= utf8.Length && Utf8EqualsAsciiIgnoreCase(utf8.Slice(pos, 3), "GMT"u8))
        {
            pos += 3;
            if (pos < utf8.Length && (utf8[pos] == (byte)'+' || utf8[pos] == (byte)'-'))
            {
                return ParseTimezoneOffset(utf8, ref pos);
            }

            return 0;
        }

        // Try +/-HH:MM
        return ParseTimezoneOffset(utf8, ref pos);
    }

    private static void SkipDayOfWeek(ReadOnlySpan<byte> utf8, ref int pos, ReadOnlySpan<byte> presentation)
    {
        if (pos >= utf8.Length)
        {
            return;
        }

        // Check for name-based (N, n, Nn, or starts with "Nn")
        if (presentation.SequenceEqual("N"u8) || presentation.SequenceEqual("n"u8) || presentation.SequenceEqual("Nn"u8) ||
            (presentation.Length >= 2 && presentation[0] == (byte)'N' && presentation[1] == (byte)'n'))
        {
            ParseName(utf8, ref pos, DayNames);
            return;
        }

        // Check for numeric
        if (utf8[pos] >= (byte)'0' && utf8[pos] <= (byte)'9')
        {
            while (pos < utf8.Length && utf8[pos] >= (byte)'0' && utf8[pos] <= (byte)'9')
            {
                pos++;
            }

            return;
        }

        // Try name anyway
        ParseName(utf8, ref pos, DayNames);
    }

    private static void SkipWord(ReadOnlySpan<byte> utf8, ref int pos)
    {
        while (pos < utf8.Length)
        {
            byte b = utf8[pos];
            if ((b | 0x20) >= (byte)'a' && (b | 0x20) <= (byte)'z')
            {
                pos++;
            }
            else
            {
                break;
            }
        }
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
    /// Appends a single char as UTF-8 bytes (handles BMP characters up to U+FFFF).
    /// </summary>
    private static ReadOnlySpan<byte> TrimAsciiWhitespace(ReadOnlySpan<byte> span)
    {
        int start = 0;
        while (start < span.Length && IsAsciiWhitespace(span[start]))
        {
            start++;
        }

        int end = span.Length;
        while (end > start && IsAsciiWhitespace(span[end - 1]))
        {
            end--;
        }

        return span.Slice(start, end - start);
    }

    private static bool IsAsciiWhitespace(byte b) => b == (byte)' ' || b == (byte)'\t' || b == (byte)'\n' || b == (byte)'\r';

    /// <summary>
    /// Copies bytes from <paramref name="source"/> to <paramref name="destination"/>, skipping ASCII whitespace.
    /// Returns the number of bytes written.
    /// </summary>
    private static int StripAllAsciiWhitespace(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        int written = 0;
        for (int i = 0; i < source.Length; i++)
        {
            byte b = source[i];
            if (!IsAsciiWhitespace(b))
            {
                destination[written++] = b;
            }
        }

        return written;
    }

    /// <summary>
    /// Decodes a single UTF-8 code point from the start of the span.
    /// Works on all TFMs without requiring System.Text.Rune.
    /// </summary>
    /// <returns><see langword="true"/> if a valid code point was decoded; <see langword="false"/> on invalid UTF-8.</returns>
    private static bool TryDecodeUtf8CodePoint(ReadOnlySpan<byte> source, out int codePoint, out int bytesConsumed)
    {
        if (source.Length == 0)
        {
            codePoint = 0;
            bytesConsumed = 0;
            return false;
        }

        byte first = source[0];

        if (first < 0x80)
        {
            codePoint = first;
            bytesConsumed = 1;
            return true;
        }

        if ((first & 0xE0) == 0xC0 && source.Length >= 2 && (source[1] & 0xC0) == 0x80)
        {
            codePoint = ((first & 0x1F) << 6) | (source[1] & 0x3F);
            bytesConsumed = 2;
            return codePoint >= 0x80;
        }

        if ((first & 0xF0) == 0xE0 && source.Length >= 3 && (source[1] & 0xC0) == 0x80 && (source[2] & 0xC0) == 0x80)
        {
            codePoint = ((first & 0x0F) << 12) | ((source[1] & 0x3F) << 6) | (source[2] & 0x3F);
            bytesConsumed = 3;
            return codePoint >= 0x800 && (codePoint < 0xD800 || codePoint > 0xDFFF);
        }

        if ((first & 0xF8) == 0xF0 && source.Length >= 4 && (source[1] & 0xC0) == 0x80 && (source[2] & 0xC0) == 0x80 && (source[3] & 0xC0) == 0x80)
        {
            codePoint = ((first & 0x07) << 18) | ((source[1] & 0x3F) << 12) | ((source[2] & 0x3F) << 6) | (source[3] & 0x3F);
            bytesConsumed = 4;
            return codePoint >= 0x10000 && codePoint <= 0x10FFFF;
        }

        codePoint = 0xFFFD;
        bytesConsumed = 1;
        return false;
    }
}