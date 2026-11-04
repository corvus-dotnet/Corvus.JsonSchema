// <copyright file="FormatMatchers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;
#if CORVUS_JSON_EXTENDEDTYPES
using Corvus.Text.Json.Internal.Globalization;
#else
using Corvus.Globalization;
#endif
#if CORVUS_TEXT_JSON_CODEGENERATION
using Corvus.Text.Json.CodeGeneration.Internal;
#endif
using NodaTime.Calendars;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// The checks of the JSON Schema string formats, on UTF-8 values, without allocating.
/// </summary>
/// <remarks>
/// <para>
/// This class and the files it uses are compiled into the V5 engine (Corvus.Text.Json) and linked into the V4 engine
/// (Corvus.Json.ExtendedTypes), so the two engines give the same answer for every format.
/// </para>
/// <para>
/// Each check reads the grammar the format names: RFC 3339 for the dates, times and durations, RFC 5321 and RFC 6531 for
/// the email addresses, RFC 1123, RFC 5890 to RFC 5893 and UTS #46 for the host names, RFC 4291 and RFC 3986 for the IP
/// addresses, RFC 3986 and RFC 3987 for the references, RFC 6570 for the templates, RFC 6901 and the relative JSON
/// pointer draft for the pointers, and RFC 4122 for the UUID.
/// </para>
/// </remarks>
internal static class FormatMatchers
{
    /// <summary>
    /// Gets the allowed characters for the local part of an email address.
    /// </summary>
    private static ReadOnlySpan<byte> AllowedLocalCharacters => "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$%&'*+-/=?^_`{|}~"u8;

    private static ReadOnlySpan<int> DisallowedIdn =>
            [0x0640, 0x07FA, 0x302E, 0x302F,
        0x3031, 0x3032, 0x3033, 0x3034,
        0x3035, 0x303B];

    private static ReadOnlySpan<int> ViramaTable =>
            [0x094D, 0x09CD, 0x0A4D, 0x0ACD,
        0x0B4D, 0x0BCD, 0x0C4D, 0x0CCD,
        0x0D3B, 0x0D3C, 0x0D4D, 0x0DCA,
        0x0E3A, 0x0EBA, 0x0F84, 0x1039,
        0x103A, 0x1714, 0x1715, 0x1734,
        0x17D2, 0x1A60, 0x1B44, 0x1BAA,
        0x1BAB, 0x1BF2, 0x1BF3, 0x2D7F,
        0xA806, 0xA82C, 0xA8C4, 0xA953,
        0xA9C0, 0xAAF6, 0xABED, 0x10A3F,
        0x11046, 0x11070, 0x1107F, 0x110B9,
        0x11133, 0x11134, 0x111C0, 0x11235,
        0x112EA, 0x1134D, 0x11442, 0x114C2,
        0x115BF, 0x1163F, 0x116B6, 0x1172B,
        0x11839, 0x1193D, 0x1193E, 0x119E0,
        0x11A34, 0x11A47, 0x11A99, 0x11C3F,
        0x11D44, 0x11D45, 0x11D97];

    private const int MinutesPerDay = 24 * 60;

    private const int LastMinuteOfDay = MinutesPerDay - 1;

    /// <summary>
    /// The UTC days that ended with a leap second, as days from 1970-01-01: every one the IERS has announced, from
    /// 1972-06-30 to 2016-12-31 (IERS Bulletin C).
    /// </summary>
    private static readonly int[] LeapSecondDays =
    [
        DaysFromCivil(1972, 6, 30), DaysFromCivil(1972, 12, 31), DaysFromCivil(1973, 12, 31),
        DaysFromCivil(1974, 12, 31), DaysFromCivil(1975, 12, 31), DaysFromCivil(1976, 12, 31),
        DaysFromCivil(1977, 12, 31), DaysFromCivil(1978, 12, 31), DaysFromCivil(1979, 12, 31),
        DaysFromCivil(1981, 6, 30), DaysFromCivil(1982, 6, 30), DaysFromCivil(1983, 6, 30),
        DaysFromCivil(1985, 6, 30), DaysFromCivil(1987, 12, 31), DaysFromCivil(1989, 12, 31),
        DaysFromCivil(1990, 12, 31), DaysFromCivil(1992, 6, 30), DaysFromCivil(1993, 6, 30),
        DaysFromCivil(1994, 6, 30), DaysFromCivil(1995, 12, 31), DaysFromCivil(1997, 6, 30),
        DaysFromCivil(1998, 12, 31), DaysFromCivil(2005, 12, 31), DaysFromCivil(2008, 12, 31),
        DaysFromCivil(2012, 6, 30), DaysFromCivil(2015, 6, 30), DaysFromCivil(2016, 12, 31),
    ];

    /// <summary>
    /// Validates that a value is an RFC 3339 <c>full-date</c>, for the <c>date</c> format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded value.</param>
    /// <returns><see langword="true"/> if the value is a date.</returns>
    internal static bool MatchDate(ReadOnlySpan<byte> value)
    {
        return value.Length == 10 &&
            JsonElementHelpers.ParseDateCore(value, out int year, out int month, out int day) &&
            GregorianYearMonthDayCalculator.TryValidateGregorianYearMonthDay(year, month, day);
    }

    /// <summary>
    /// Validates that a value is an RFC 3339 <c>full-time</c>, for the <c>time</c> format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded value.</param>
    /// <returns><see langword="true"/> if the value is a time.</returns>
    /// <remarks>
    /// A second of <c>60</c> (a leap second) is accepted when the time is 23:59 in UTC. A time has no date, so the
    /// date of the leap second is not checked.
    /// </remarks>
    internal static bool MatchTime(ReadOnlySpan<byte> value)
    {
        if (!TryParseFullTime(value, out int hour, out int minute, out int second, out int offsetMinutes))
        {
            return false;
        }

        if (second < 60)
        {
            return true;
        }

        // The UTC minute runs from -1439 to 2878, so 23:59 UTC is minute 1439 of this day or minute -1 (the day before).
        int utcMinute = (hour * 60) + minute - offsetMinutes;
        return utcMinute == LastMinuteOfDay || utcMinute == LastMinuteOfDay - MinutesPerDay;
    }

    /// <summary>
    /// Validates that a value is an RFC 3339 <c>date-time</c>, for the <c>date-time</c> format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded value.</param>
    /// <returns><see langword="true"/> if the value is a date-time.</returns>
    /// <remarks>
    /// <para>
    /// A lowercase <c>t</c> separator is accepted, as RFC 3339 section 5.6 allows.
    /// </para>
    /// <para>
    /// A second of <c>60</c> (a leap second) is accepted only at 23:59:60 UTC on a day the IERS has ended with a
    /// leap second (RFC 3339 section 5.7).
    /// </para>
    /// </remarks>
    internal static bool MatchDateTime(ReadOnlySpan<byte> value)
    {
        // YYYY-MM-DDTHH:MM:SS, then at least the one character of a "Z" offset.
        if (value.Length < 20 ||
            (value[10] != (byte)'T' && value[10] != (byte)'t') ||
            !JsonElementHelpers.ParseDateCore(value, out int year, out int month, out int day) ||
            !GregorianYearMonthDayCalculator.TryValidateGregorianYearMonthDay(year, month, day) ||
            !TryParseFullTime(value.Slice(11), out int hour, out int minute, out int second, out int offsetMinutes))
        {
            return false;
        }

        if (second < 60)
        {
            return true;
        }

        int utcMinute = (hour * 60) + minute - offsetMinutes;
        int dayShift = utcMinute < 0 ? -1 : utcMinute >= MinutesPerDay ? 1 : 0;
        if (utcMinute - (dayShift * MinutesPerDay) != LastMinuteOfDay)
        {
            return false;
        }

        return IsLeapSecondDay(DaysFromCivil(year, month, day) + dayShift);
    }

    /// <summary>
    /// Reads an RFC 3339 <c>full-time</c>: <c>HH:MM:SS</c>, an optional fraction of any length, and a <c>Z</c> or
    /// <c>+HH:MM</c> offset. The hour is 00 to 23, the minute 00 to 59, the second 00 to 60, and the offset hour
    /// 00 to 23.
    /// </summary>
    private static bool TryParseFullTime(ReadOnlySpan<byte> value, out int hour, out int minute, out int second, out int offsetMinutes)
    {
        hour = 0;
        minute = 0;
        second = 0;
        offsetMinutes = 0;

        // HH:MM:SS, then at least the one character of a "Z" offset.
        if (value.Length < 9 ||
            value[2] != (byte)':' ||
            value[5] != (byte)':' ||
            !TryReadTwoDigits(value, 0, out hour) ||
            !TryReadTwoDigits(value, 3, out minute) ||
            !TryReadTwoDigits(value, 6, out second) ||
            hour > 23 ||
            minute > 59 ||
            second > 60)
        {
            return false;
        }

        int index = 8;
        if (value[index] == (byte)'.')
        {
            index++;
            int fractionStart = index;
            while (index < value.Length && (uint)(value[index] - '0') <= 9)
            {
                index++;
            }

            if (index == fractionStart)
            {
                return false;
            }
        }

        ReadOnlySpan<byte> offset = value.Slice(index);
        if (offset.Length == 1)
        {
            return offset[0] == (byte)'Z' || offset[0] == (byte)'z';
        }

        if (offset.Length != 6 ||
            (offset[0] != (byte)'+' && offset[0] != (byte)'-') ||
            offset[3] != (byte)':' ||
            !TryReadTwoDigits(offset, 1, out int offsetHour) ||
            !TryReadTwoDigits(offset, 4, out int offsetMinute) ||
            offsetHour > 23 ||
            offsetMinute > 59)
        {
            return false;
        }

        offsetMinutes = (offsetHour * 60) + offsetMinute;
        if (offset[0] == (byte)'-')
        {
            offsetMinutes = -offsetMinutes;
        }

        return true;
    }

    private static bool TryReadTwoDigits(ReadOnlySpan<byte> value, int index, out int result)
    {
        uint tens = (uint)(value[index] - '0');
        uint units = (uint)(value[index + 1] - '0');
        if (tens > 9 || units > 9)
        {
            result = 0;
            return false;
        }

        result = (int)((tens * 10) + units);
        return true;
    }

    /// <summary>
    /// Gets the number of days from 1970-01-01 to a proleptic Gregorian date.
    /// </summary>
    private static int DaysFromCivil(int year, int month, int day)
    {
        // Howard Hinnant's days_from_civil.
        year -= month <= 2 ? 1 : 0;
        int era = (year >= 0 ? year : year - 399) / 400;
        int yearOfEra = year - (era * 400);
        int dayOfYear = ((((153 * (month + (month > 2 ? -3 : 9))) + 2) / 5) + day) - 1;
        int dayOfEra = (yearOfEra * 365) + (yearOfEra / 4) - (yearOfEra / 100) + dayOfYear;
        return (era * 146097) + dayOfEra - 719468;
    }

    private static bool IsLeapSecondDay(int daysFromEpoch)
    {
        foreach (int leapDay in LeapSecondDays)
        {
            if (leapDay == daysFromEpoch)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates that a decoded UTF-8 hostname value conforms to the hostname format requirements.
    /// </summary>
    /// <param name="value">The decoded UTF-8 hostname value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid decoded hostname; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchDecodedHostname(ReadOnlySpan<byte> value)
    {
        bool wasLastDot = false;
        int i = 0;
        Rune previousRune = default;
        bool hasHiraganaKatakanaOrHan = false;
        bool hasKatakankaMiddleDot = false;
        bool hasArabicIndicDigits = false;
        bool hasExtendedArabicIndicDigits = false;
        int runeCount = 0;
        int labelStart = 0;
        bool labelHasNonAscii = false;
        bool labelFirstSet = false;
        bool labelFirstIsL = false;
        bool labelFirstIsRal = false;
        bool labelHasL = false;
        bool labelHasRal = false;
        bool labelHasEuropeanDigit = false;
        bool domainIsBidi = false;
        bool bidiViolation = false;
        int nameLength = 0;
        while (i < value.Length)
        {
            // Increment the rune count to make it 1-based
            runeCount++;

            byte byteValue = value[i];
            Rune.DecodeFromUtf8(value.Slice(i), out Rune rune, out int bytesConsumed);

            StrongBidiCategory bidiCategory = rune.Value < 0x80
                ? (((byteValue | 0x20) >= (byte)'a' && (byteValue | 0x20) <= (byte)'z') ? StrongBidiCategory.StrongLeftToRight : StrongBidiCategory.Other)
                : CharUnicodeInfo.GetBidiCategory(value, i, out _);

            if (i == 0)
            {
                System.Globalization.UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                if (category == System.Globalization.UnicodeCategory.SpacingCombiningMark ||
                    category == System.Globalization.UnicodeCategory.EnclosingMark ||
                    category == System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    return false;
                }
            }

            i += bytesConsumed;

            hasHiraganaKatakanaOrHan |= IsHiraganaKatakanaOrHanNotMiddleDot(rune.Value);
            hasArabicIndicDigits |= IsArabicIndicDigit(rune.Value);
            hasExtendedArabicIndicDigits |= IsExtendedArabicIndicDigit(rune.Value);

            bool isDotRune = byteValue == (byte)'.' || rune.Value == 0x3002 || rune.Value == 0xFF0E || rune.Value == 0xFF61;

            if (!isDotRune)
            {
                // Track the RFC 5893 Bidi properties of the label. The rules are evaluated at
                // the end of each label, and enforced only when the domain turns out to be a
                // Bidi domain name (it contains at least one RTL character anywhere).
                if (!labelFirstSet)
                {
                    labelFirstSet = true;
                    labelFirstIsL = bidiCategory == StrongBidiCategory.StrongLeftToRight;
                    labelFirstIsRal = bidiCategory == StrongBidiCategory.StrongRightToLeft;
                }

                labelHasL |= bidiCategory == StrongBidiCategory.StrongLeftToRight;
                labelHasRal |= bidiCategory == StrongBidiCategory.StrongRightToLeft;
                labelHasEuropeanDigit |= (byteValue >= (byte)'0' && byteValue <= (byte)'9') || IsExtendedArabicIndicDigit(rune.Value);
                labelHasNonAscii |= rune.Value > 0x7F;
                domainIsBidi |= bidiCategory == StrongBidiCategory.StrongRightToLeft || IsArabicIndicDigit(rune.Value);
            }

            if (wasLastDot)
            {
                if (!Rune.IsLetter(rune))
                {
                    return false;
                }
            }

            if (DisallowedIdn.IndexOf(rune.Value) >= 0)
            {
                // Disallowed characters in IDN
                return false;
            }

            if (!Rune.IsLetterOrDigit(rune))
            {
                if (isDotRune)
                {
                    if (hasKatakankaMiddleDot && !hasHiraganaKatakanaOrHan)
                    {
                        // If we have a Katakana middle dot, it must be have a Hiragana, Katakana or Han character.
                        return false;
                    }

                    if (hasArabicIndicDigits && hasExtendedArabicIndicDigits)
                    {
                        // You are not permitted both arabic indic AND extended arabic indic
                        return false;
                    }

                    if (!AddALabelLength(value.Slice(labelStart, i - bytesConsumed - labelStart), labelHasNonAscii, ref nameLength))
                    {
                        // The A-label form of this U-label exceeds the 63-octet label limit (RFC 5890)
                        return false;
                    }

                    // The dot, which is one character as A-labels whichever of the four dots it is.
                    nameLength++;

                    if (labelFirstSet)
                    {
                        bidiViolation |= IsBidiLabelViolation(labelFirstIsL, labelFirstIsRal, labelHasL, labelHasRal, hasArabicIndicDigits, labelHasEuropeanDigit);
                    }

                    hasHiraganaKatakanaOrHan = false;
                    hasKatakankaMiddleDot = false;
                    hasArabicIndicDigits = false;
                    hasExtendedArabicIndicDigits = false;
                    labelStart = i;
                    labelHasNonAscii = false;
                    labelFirstSet = false;
                    labelFirstIsL = false;
                    labelFirstIsRal = false;
                    labelHasL = false;
                    labelHasRal = false;
                    labelHasEuropeanDigit = false;
                    wasLastDot = true;
                    previousRune = rune;
                    continue;
                }

                hasKatakankaMiddleDot |= (rune.Value == 0x30FB);

                // First we do all the items that are allowed at the first character
                Rune lookahead = default;

                if (rune.Value == 0x0375)
                {
                    // If we have a Greek Keraia, it must be followed by a Greek character.
                    // If we are the last character, that's a fail
                    if (i >= value.Length)
                    {
                        return false;
                    }

                    Rune.DecodeFromUtf8(value.Slice(i), out lookahead, out _);

                    // If the next character is not Greek, that's a fail
                    if (!IsGreek(lookahead.Value))
                    {
                        return false;
                    }

                    wasLastDot = false;
                    previousRune = rune;
                    continue;
                }

                // Middle dot
                if (rune.Value == 0x00B7)
                {
                    if (previousRune.Value != 0x006C)
                    {
                        return false;
                    }

                    if (lookahead.Value == 0)
                    {
                        Rune.DecodeFromUtf8(value.Slice(i), out lookahead, out _);
                        if (lookahead.Value != 0x006C)
                        {
                            return false;
                        }
                    }
                }

                // These are all the tests which require preceding characters
                if (byteValue == (byte)'-')
                {
                    if (i == bytesConsumed || i >= value.Length)
                    {
                        return false;
                    }

                    // A label does not end with a hyphen, so a hyphen cannot come before a dot.
                    Rune.DecodeFromUtf8(value.Slice(i), out Rune afterHyphen, out _);
                    if (afterHyphen.Value is '.' or 0x3002 or 0xFF0E or 0xFF61)
                    {
                        return false;
                    }

                    // Positions 3 and 4 must not be '--'
                    if (runeCount == 4 && previousRune.Value == 0x002d)
                    {
                        return false;
                    }

                    wasLastDot = false;
                    previousRune = rune;
                    continue;
                }

                if (rune.Value < 0x80)
                {
                    // ASCII other than letters, digits, '.' and '-' (all handled above) is not valid
                    // in a hostname label; without this, symbols like '[', ':' or '_' would fall
                    // through the Unicode contextual rules below and be accepted.
                    return false;
                }

                // ZERO WIDTH JOINER not preceded by Virama
                if (rune.Value == 0x200D && !IsVirama(previousRune.Value))
                {
                    return false;
                }

                // ZERO WIDTH NON-JOINER must be preceded by Virama, or occur in a joining
                // context (RFC 5892 Appendix A.1). This applies at every occurrence.
                if (rune.Value == 0x200C &&
                    !IsVirama(previousRune.Value) &&
                    !IsZwnjInJoiningContext(value, labelStart, i - bytesConsumed, i))
                {
                    return false;
                }

                // If we are Geresh or Gershayim, the previous rune must be Hebrew
                if ((rune.Value == 0x05F3 || rune.Value == 0x05F4) && !IsHebrew(previousRune.Value))
                {
                    return false;
                }

                if (rune.Value > 0x7F && !IsPermittedNonLetterOrDigit(rune.Value))
                {
                    System.Globalization.UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                    if (category != System.Globalization.UnicodeCategory.NonSpacingMark &&
                        category != System.Globalization.UnicodeCategory.SpacingCombiningMark &&
                        category != System.Globalization.UnicodeCategory.EnclosingMark)
                    {
                        // IDNA2008 disallows punctuation, symbols and other non-letter,
                        // non-digit, non-mark code points in a U-label, except the contextual
                        // code points handled above.
                        return false;
                    }
                }
            }

            wasLastDot = false;
            previousRune = rune;
        }

        if (hasKatakankaMiddleDot && !hasHiraganaKatakanaOrHan)
        {
            // If we have a Katakana middle dot, it must be have a Hiragana, Katakana or Han character.
            return false;
        }

        if (hasArabicIndicDigits && hasExtendedArabicIndicDigits)
        {
            // You are not permitted both Arabic Indic AND extended Arabic Indic
            return false;
        }

        if (!AddALabelLength(value.Slice(labelStart), labelHasNonAscii, ref nameLength))
        {
            // The A-label form of this U-label exceeds the 63-octet label limit (RFC 5890)
            return false;
        }

        if (nameLength > MaxHostnameLength)
        {
            // The name as A-labels is longer than 253 characters (RFC 5890 and RFC 1034)
            return false;
        }

        if (labelFirstSet)
        {
            bidiViolation |= IsBidiLabelViolation(labelFirstIsL, labelFirstIsRal, labelHasL, labelHasRal, hasArabicIndicDigits, labelHasEuropeanDigit);
        }

        if (domainIsBidi && bidiViolation)
        {
            // RFC 5893 Bidi rule violation in a Bidi domain name
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether a value matches the RFC 3339 Appendix A <c>duration</c> grammar.
    /// </summary>
    /// <param name="value">The UTF-8 encoded value.</param>
    /// <returns><see langword="true"/> if the value is a duration.</returns>
    /// <remarks>
    /// <para>
    /// The check is of the grammar alone: a component may have any number of digits, as the grammar allows, even
    /// where the value would not fit a period value.
    /// </para>
    /// <code>
    /// duration   = "P" (dur-date / dur-time / dur-week)
    /// dur-date   = (dur-day / dur-month / dur-year) [dur-time]
    /// dur-time   = "T" (dur-hour / dur-minute / dur-second)
    /// dur-year   = 1*DIGIT "Y" [dur-month]
    /// dur-month  = 1*DIGIT "M" [dur-day]
    /// dur-day    = 1*DIGIT "D"
    /// dur-hour   = 1*DIGIT "H" [dur-minute]
    /// dur-minute = 1*DIGIT "M" [dur-second]
    /// dur-second = 1*DIGIT "S"
    /// dur-week   = 1*DIGIT "W"
    /// </code>
    /// </remarks>
    internal static bool MatchDuration(ReadOnlySpan<byte> value)
    {
        if (value.Length < 3 || value[0] != (byte)'P')
        {
            return false;
        }

        int index = 1;

        // The previous unit in the current part: 0 at the start of a part, then the unit letter.
        byte previous = 0;
        bool inTime = false;
        bool any = false;

        while (index < value.Length)
        {
            if (value[index] == (byte)'T')
            {
                if (inTime)
                {
                    return false;
                }

                inTime = true;
                previous = 0;
                index++;

                // "T" must introduce at least one time element.
                if (index == value.Length)
                {
                    return false;
                }

                continue;
            }

            int digitsStart = index;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
                index++;
            }

            if (index == digitsStart || index == value.Length)
            {
                return false;
            }

            byte unit = value[index++];
            bool allowed = inTime
                ? unit switch
                {
                    (byte)'H' => previous == 0,
                    (byte)'M' => previous is 0 or (byte)'H',
                    (byte)'S' => previous is 0 or (byte)'M',
                    _ => false,
                }
                : unit switch
                {
                    (byte)'Y' => previous == 0,
                    (byte)'M' => previous is 0 or (byte)'Y',
                    (byte)'D' => previous is 0 or (byte)'M',

                    // A week stands alone: it is the only element of the duration.
                    (byte)'W' => !any && index == value.Length,
                    _ => false,
                };

            if (!allowed)
            {
                return false;
            }

            previous = unit;
            any = true;
        }

        return any;
    }

    /// <summary>
    /// Validates that a string value is a valid email address format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid email address; otherwise, <see langword="false"/>.</returns>
    internal static bool MatchEmail(ReadOnlySpan<byte> value)
    {
        // There is no limit on the address as a whole, or on its local part: only the domain has one (253 characters,
        // as a host name). The other ports limit only the domain too (#1029).
        if (value.Length < 3)
        {
            return false;
        }

        int atIndex = value.LastIndexOf((byte)'@');
        if (atIndex <= 0 || atIndex == value.Length - 1)
        {
            return false;
        }

        // Local part
        ReadOnlySpan<byte> segment = value.Slice(0, atIndex);

        if (!MatchEmailLocalPart(segment))
        {
            return false;
        }

        // Domain part
        segment = value.Slice(atIndex + 1);

        if (segment[0] == (byte)'[')
        {
            return MatchAddressLiteral(segment);
        }

        return MatchHostname(segment);
    }

    /// <summary>
    /// Validates that an email domain segment is a valid RFC 5321 address-literal.
    /// </summary>
    /// <param name="segment">The UTF-8 encoded domain segment, including the enclosing brackets.</param>
    /// <returns><see langword="true"/> if the segment is a valid address-literal; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// RFC 5321 §4.1.3: <c>address-literal = "[" ( IPv4-address-literal / IPv6-address-literal / General-address-literal ) "]"</c>.
    /// General-address-literal tags are not accepted here; only IPv4 and IPv6 literals validate, matching the
    /// JSON Schema test suite's expectations for the <c>email</c> and <c>idn-email</c> formats.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchAddressLiteral(ReadOnlySpan<byte> segment)
    {
        if (segment.Length < 3 || segment[segment.Length - 1] != (byte)']')
        {
            return false;
        }

        ReadOnlySpan<byte> addressLiteral = segment.Slice(1, segment.Length - 2);

        // The "IPv6:" tag is a literal string in the RFC 5321 grammar, so it matches in any case (RFC 5234).
        if (addressLiteral.Length >= 5 &&
            (addressLiteral[0] | 0x20) == (byte)'i' &&
            (addressLiteral[1] | 0x20) == (byte)'p' &&
            (addressLiteral[2] | 0x20) == (byte)'v' &&
            addressLiteral[3] == (byte)'6' &&
            addressLiteral[4] == (byte)':')
        {
            return MatchIPV6(addressLiteral.Slice(5));
        }

        return MatchIPV4AddressLiteral(addressLiteral);
    }

    /// <summary>
    /// Validates that a value is a valid RFC 5321 IPv4-address-literal.
    /// </summary>
    /// <param name="value">The UTF-8 encoded value to validate, without the enclosing brackets.</param>
    /// <returns><see langword="true"/> if the value is a valid IPv4-address-literal; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// RFC 5321 §4.1.3: <c>IPv4-address-literal = Snum 3("." Snum)</c> where <c>Snum = 1*3DIGIT</c> representing a
    /// decimal value in the range 0 through 255. Unlike the RFC 3986 dec-octet rule used for the <c>ipv4</c> format,
    /// Snum permits leading zeros, and they do not change the base — so this cannot be delegated to
    /// <see cref="IPAddressParser.IsValidIPV4(ReadOnlySpan{byte}, bool)"/> in either canonical mode (which rejects
    /// leading zeros) or non-canonical mode (which treats a leading zero as octal and accepts hex and shortened forms).
    /// </remarks>
    private static bool MatchIPV4AddressLiteral(ReadOnlySpan<byte> value)
    {
        int dots = 0;
        int digits = 0;
        int octetValue = 0;

        for (int i = 0; i < value.Length; i++)
        {
            byte current = value[i];

            if (current == (byte)'.')
            {
                if (digits == 0 || dots == 3)
                {
                    return false;
                }

                dots++;
                digits = 0;
                octetValue = 0;
                continue;
            }

            uint digit = (uint)(current - (byte)'0');

            if (digit > 9 || digits == 3)
            {
                return false;
            }

            digits++;
            octetValue = (octetValue * 10) + (int)digit;

            if (octetValue > 255)
            {
                return false;
            }
        }

        return dots == 3 && digits != 0;
    }

    /// <summary>
    /// The most characters a host name has as A-labels, not counting a final dot (RFC 1034 and RFC 5890).
    /// </summary>
    private const int MaxHostnameLength = 253;

    /// <summary>
    /// The most characters a label has as an A-label (RFC 5890).
    /// </summary>
    private const int MaxLabelLength = 63;

    /// <summary>
    /// The most UTF-8 bytes a host name has in any form. A name is <see cref="MaxHostnameLength"/> characters at most
    /// as A-labels, and no label is shorter as an A-label than it has characters: an ASCII letter is itself there,
    /// any other character is a digit or more, and <c>xn--</c> comes before them. So a name has 253 characters at
    /// most, each of four bytes at most, in its Unicode form, in its A-label form or in a mix of the two. A longer
    /// string is not a host name, and is not read.
    /// </summary>
    private const int MaxHostnameBytes = 4 * MaxHostnameLength;

    /// <summary>
    /// Validates that a string value is a valid hostname format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid hostname; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchHostname(ReadOnlySpan<byte> value)
    {
        if (value.Length > MaxHostnameLength)
        {
            return false;
        }

        // The A-labels of the name decode to its Unicode form, which can be longer (see MaxHostnameBytes).
        Span<byte> decoded = stackalloc byte[MaxHostnameBytes];
        int i = 0;
        int characterCount = 0;
        byte lastAscii = 0;
        bool decodePunicode = false;
        while (i < value.Length)
        {
            if (value[i] > 0x7F)
            {
                // This is not ASCII, so give up
                return false;
            }

            if (lastAscii == (byte)'-' && value[i] == (byte)'-')
            {
                // Detect punycode ACE prefix "xn--" at positions 3-4 of a label
                if (characterCount == 3 &&
                    (value[i - 3] == (byte)'x' || value[i - 3] == (byte)'X') &&
                    (value[i - 2] == (byte)'n' || value[i - 2] == (byte)'N'))
                {
                    decodePunicode = true;
                    break;
                }

                // RFC 1123 allows consecutive hyphens — continue processing
            }

            byte previous = lastAscii;
            lastAscii = value[i];

            if (lastAscii == (byte)'.')
            {
                // A label is 1 to 63 characters, and does not end with a hyphen (RFC 1123), so a dot cannot follow
                // a dot (an empty label) or a hyphen.
                if (characterCount == 0 || characterCount > 63 || previous == (byte)'-')
                {
                    return false;
                }

                characterCount = 0;
                i++;
                continue;
            }

            if (!char.IsLetterOrDigit((char)lastAscii) && !(characterCount != 0 && lastAscii == (byte)'-'))
            {
                return false;
            }

            characterCount++;
            i++;
        }

        if (decodePunicode)
        {
            if (!IdnMapping.GetUnicodeOfAnyNameLength(value, decoded, out int written))
            {
                return false;
            }

            scoped ReadOnlySpan<byte> segment = decoded.Slice(0, written);

            return MatchDecodedHostname(segment);
        }

        if (characterCount == 0 || characterCount > 63)
        {
            return false;
        }

        if (lastAscii == '-' || lastAscii == '.')
        {
            return false;
        }

        if (value[0] == '.')
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid internationalized domain name (IDN) email address format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid IDN email address; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIdnEmail(ReadOnlySpan<byte> value)
    {
        // There is no limit on the address as a whole, or on its local part: only the domain has one, the limit of a
        // host name (253 characters as A-labels), which a domain of U-labels can meet with more than 320 bytes of UTF-8
        // in the address. The other ports limit only the domain too (#1029).
        if (value.Length < 3)
        {
            return false;
        }

        int atIndex = value.LastIndexOf((byte)'@');

        if (atIndex <= 0 || atIndex == value.Length - 1)
        {
            return false;
        }

        // Local part
        ReadOnlySpan<byte> segment = value.Slice(0, atIndex);

        if (!MatchEmailLocalPartUnicode(segment))
        {
            return false;
        }

        // Domain part
        segment = value.Slice(atIndex + 1);

        if (segment[0] == (byte)'[')
        {
            // RFC 6531 extends only the local part; the RFC 5321 address-literal production is unchanged.
            // This must not fall through to MatchIdnHostname, which does not reject '[', ':' or ']'.
            return MatchAddressLiteral(segment);
        }

        return MatchIdnHostname(segment);
    }

    /// <summary>
    /// Validates that a string value is a valid internationalized domain name (IDN) hostname format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid IDN hostname; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIdnHostname(ReadOnlySpan<byte> value)
    {
        // The limit of 253 is on the name as A-labels, which MatchDecodedHostname counts. A name of U-labels can have
        // more UTF-8 bytes than that, and fewer.
        if (value.Length > MaxHostnameBytes)
        {
            return false;
        }

        // UTS #46 maps the name before it is checked: it removes the ignored code points and replaces the mapped
        // ones, such as the fullwidth forms, which can produce an "xn--" A-label to decode. A mapped name longer than
        // the most bytes a host name has is not a host name.
        Span<byte> mapped = stackalloc byte[MaxHostnameBytes];
        scoped ReadOnlySpan<byte> name = value;
        if (Uts46Mapping.MayChange(value))
        {
            if (!Uts46Mapping.TryMap(value, mapped, out int mappedLength))
            {
                return false;
            }

            name = mapped.Slice(0, mappedLength);
        }

        Span<byte> decoded = stackalloc byte[MaxHostnameBytes];

        if (!IdnMapping.GetUnicodeOfAnyNameLength(name, decoded, out int written))
        {
            return false;
        }

        scoped ReadOnlySpan<byte> segment = decoded.Slice(0, written);

        return MatchDecodedHostname(segment);
    }

    /// <summary>
    /// Validates that a string value is a valid IPv4 address format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid IPv4 address; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIPV4(ReadOnlySpan<byte> value)
    {
        if (value.Length > IPAddressParser.MaxIPv4StringLength)
        {
            return false;
        }

        return IPAddressParser.IsValidIPV4(value);
    }

    /// <summary>
    /// Validates that a string value is a valid IPv6 address format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid IPv6 address; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIPV6(ReadOnlySpan<byte> value)
    {
        // The brackets of an IP-literal belong to a URI host, not to the address (RFC 4291 section 2.2).
        if (value.Length > IPAddressParser.MaxIPv6StringLength || (value.Length > 0 && value[0] == (byte)'['))
        {
            return false;
        }

        return IPAddressParser.IsValidIPV6(value);
    }

    /// <summary>
    /// Validates that a string value is a valid Internationalized Resource Identifier (IRI) format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid IRI; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIri(ReadOnlySpan<byte> value)
    {
        // An absolute IRI (RFC 3987).
        return Utf8UriGrammar.MatchAbsolute(value, iri: true);
    }

    /// <summary>
    /// Validates that a string value is a valid Internationalized Resource Identifier (IRI) reference format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid IRI reference; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIriReference(ReadOnlySpan<byte> value)
    {
        // An IRI-reference (RFC 3987).
        return Utf8UriGrammar.MatchReference(value, iri: true);
    }

    /// <summary>
    /// Validates that a string value is a valid JSON Pointer format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid JSON Pointer; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchJsonPointer(ReadOnlySpan<byte> value)
    {
        return Utf8JsonPointerTools.Validate(value);
    }

    /// <summary>
    /// Validates that a string value is a valid relative JSON Pointer format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid relative JSON Pointer; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchRelativeJsonPointer(ReadOnlySpan<byte> value)
    {
        return Utf8JsonPointerTools.ValidateRelative(value);
    }

    /// <summary>
    /// Validates that a string value is a valid URI format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid URI; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchUri(ReadOnlySpan<byte> value)
    {
        // An absolute URI (RFC 3986).
        return Utf8UriGrammar.MatchAbsolute(value, iri: false);
    }

    /// <summary>
    /// Validates that a string value is a valid URI reference format (absolute or relative URI).
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid URI reference; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchUriReference(ReadOnlySpan<byte> value)
    {
        // A URI-reference (RFC 3986).
        return Utf8UriGrammar.MatchReference(value, iri: false);
    }

    /// <summary>
    /// Validates that a string value is a valid URI template format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid URI template; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchUriTemplate(ReadOnlySpan<byte> value)
    {
        return Utf8UriTemplate.Validate(value);
    }

    /// <summary>
    /// Validates that a string value is a valid UUID format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid UUID; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchUuid(ReadOnlySpan<byte> value)
    {
        return Utf8Parser.TryParse(value, out Guid _, out int bytesConsumed) && bytesConsumed == value.Length;
    }

    private static bool IsArabicIndicDigit(int value) => (value >= 0x0660 && value <= 0x0669);

    private static bool IsExtendedArabicIndicDigit(int value) => (value >= 0x06F0 && value <= 0x06F9);

    private static bool IsGreek(int value) => (value >= 0x0370 && value <= 0x03FF) || (value >= 0x1F00 && value <= 0x1FFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHebrew(int value) => (value >= 0x0590 && value <= 0x05FF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHiraganaKatakanaOrHanNotMiddleDot(int value)
    {
        // Don't allow middle dot
        if (value == 0x30FB)
        {
            return false;
        }

        return
            (value >= 0x30A0 && value <= 0x30FF) ||
            (value >= 0x3040 && value <= 0x309F) ||
            (value >= 0x3400 && value <= 0x4DB5) ||
            (value >= 0x4E00 && value <= 0x9FCB) ||
            (value >= 0xF900 && value <= 0xFA6A);
    }

    private static bool IsVirama(int value) => ViramaTable.IndexOf(value) >= 0;

    // The RFC 5892 §2.6 PVALID exceptions and the contextual code points that are given their
    // own rules in MatchDecodedHostname. These are permitted even though they are not letters,
    // digits or marks.
    private static bool IsPermittedNonLetterOrDigit(int value) =>
        value == 0x00B7 || value == 0x0375 || value == 0x05F3 || value == 0x05F4 ||
        value == 0x06FD || value == 0x06FE || value == 0x0F0B || value == 0x200C ||
        value == 0x200D || value == 0x3007 || value == 0x30FB;

    // Joining-type approximations for the ZWNJ contextual rule (RFC 5892 Appendix A.1),
    // covering the Arabic script where the rule applies in practice.
    private static bool IsDualJoiningArabic(int value) =>
        value == 0x0620 || value == 0x0626 || value == 0x0628 ||
        (value >= 0x062A && value <= 0x062E) ||
        (value >= 0x0633 && value <= 0x063A) ||
        (value >= 0x0641 && value <= 0x0647) ||
        value == 0x0649 || value == 0x064A ||
        value == 0x067E || value == 0x0686 || value == 0x06A9 || value == 0x06AF || value == 0x06CC;

    private static bool IsRightJoiningArabic(int value) =>
        (value >= 0x0622 && value <= 0x0625) || value == 0x0627 || value == 0x0629 ||
        (value >= 0x062F && value <= 0x0632) || value == 0x0648 ||
        (value >= 0x0688 && value <= 0x0699) || value == 0x06C0 ||
        (value >= 0x06C3 && value <= 0x06CB) || value == 0x06CD || value == 0x06D2 || value == 0x06D3;

    private static bool IsZwnjInJoiningContext(ReadOnlySpan<byte> value, int labelStart, int zwnjStart, int zwnjEnd)
    {
        // RFC 5892 Appendix A.1: ZWNJ is permitted between characters that join across it — a
        // left- or dual-joining character before it and a right- or dual-joining character
        // after it, in both directions skipping transparent (non-spacing mark) characters.
        int index = zwnjStart;
        bool satisfiedBefore = false;
        while (index > labelStart)
        {
            int start = index - 1;
            while (start > labelStart && (value[start] & 0xC0) == 0x80)
            {
                start--;
            }

            Rune.DecodeFromUtf8(value.Slice(start), out Rune rune, out _);

            if (Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                index = start;
                continue;
            }

            satisfiedBefore = IsDualJoiningArabic(rune.Value);
            break;
        }

        if (!satisfiedBefore)
        {
            return false;
        }

        int forward = zwnjEnd;
        while (forward < value.Length)
        {
            Rune.DecodeFromUtf8(value.Slice(forward), out Rune rune, out int consumed);

            if (Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                forward += consumed;
                continue;
            }

            return IsDualJoiningArabic(rune.Value) || IsRightJoiningArabic(rune.Value);
        }

        return false;
    }

    // RFC 5893 §2 Bidi rules, evaluated per label. The first character determines the label
    // direction (rule 1: it must be L, R or AL); an LTR label may not contain RTL characters
    // (rule 5); an RTL label may not contain LTR characters (rule 2) and may not mix European
    // and Arabic-Indic digits (rule 4).
    private static bool IsBidiLabelViolation(bool labelFirstIsL, bool labelFirstIsRal, bool labelHasL, bool labelHasRal, bool labelHasArabicIndicDigits, bool labelHasEuropeanDigit)
    {
        return (!labelFirstIsL && !labelFirstIsRal) ||
            (labelFirstIsL && labelHasRal) ||
            (labelFirstIsRal && labelHasL) ||
            (labelHasArabicIndicDigits && labelHasEuropeanDigit);
    }

    /// <summary>
    /// Adds the length of a label as an A-label to the length of the name, and says whether the label is within the
    /// limit of 63 characters of an A-label (RFC 5890). An ASCII label is its own A-label.
    /// </summary>
    private static bool AddALabelLength(ReadOnlySpan<byte> label, bool hasNonAscii, ref int nameLength)
    {
        int length = hasNonAscii ? ALabelLength(label) : label.Length;
        nameLength += length;
        return !hasNonAscii || length <= MaxLabelLength;
    }

    /// <summary>
    /// The length of the A-label (Punycode) form of a U-label, or <see cref="MaxLabelLength"/> + 1 when it is longer
    /// than the 63 characters RFC 5890 allows, found by running the RFC 3492 encoding algorithm and counting what it
    /// writes, without writing it.
    /// </summary>
    private static int ALabelLength(ReadOnlySpan<byte> label)
    {
        Span<int> codePoints = stackalloc int[63];
        int count = 0;
        int basicCount = 0;
        int i = 0;

        while (i < label.Length)
        {
            Rune.DecodeFromUtf8(label.Slice(i), out Rune rune, out int consumed);
            i += consumed;

            if (count == codePoints.Length)
            {
                // More than 63 code points cannot fit in 63 octets once encoded.
                return MaxLabelLength + 1;
            }

            codePoints[count++] = rune.Value;

            if (rune.Value < 0x80)
            {
                basicCount++;
            }
        }

        // "xn--", the copied basic characters, and the delimiter if there are any.
        int length = 4 + basicCount + (basicCount > 0 ? 1 : 0);

        if (length > MaxLabelLength)
        {
            return MaxLabelLength + 1;
        }

        int handled = basicCount;
        int delta = 0;
        int n = 0x80;
        int bias = 72;
        bool firstTime = true;

        while (handled < count)
        {
            int m = int.MaxValue;

            for (int j = 0; j < count; j++)
            {
                if (codePoints[j] >= n && codePoints[j] < m)
                {
                    m = codePoints[j];
                }
            }

            delta += (m - n) * (handled + 1);
            n = m;

            for (int j = 0; j < count; j++)
            {
                int c = codePoints[j];

                if (c < n)
                {
                    delta++;
                }
                else if (c == n)
                {
                    int q = delta;

                    for (int k = 36; ; k += 36)
                    {
                        int t = k <= bias ? 1 : (k >= bias + 26 ? 26 : k - bias);

                        if (q < t)
                        {
                            break;
                        }

                        if (++length > MaxLabelLength)
                        {
                            return MaxLabelLength + 1;
                        }

                        q = (q - t) / (36 - t);
                    }

                    if (++length > MaxLabelLength)
                    {
                        return MaxLabelLength + 1;
                    }

                    bias = PunycodeAdapt(delta, handled + 1, firstTime);
                    firstTime = false;
                    delta = 0;
                    handled++;
                }
            }

            delta++;
            n++;
        }

        return length;
    }

    private static int PunycodeAdapt(int delta, int numPoints, bool firstTime)
    {
        // RFC 3492 §6.1 with the standard base=36, tmin=1, tmax=26, damp=700, skew=38.
        delta = firstTime ? delta / 700 : delta / 2;
        delta += delta / numPoints;

        int k = 0;
        while (delta > 455)
        {
            delta /= 35;
            k += 36;
        }

        return k + ((36 * delta) / (delta + 38));
    }

    /// <summary>
    /// Validates an RFC 5321 <c>Local-part</c>, for the <c>email</c> format.
    /// </summary>
    /// <param name="segment">The UTF-8 encoded local part (the text before the final <c>@</c>).</param>
    /// <returns><see langword="true"/> if the segment is a local part.</returns>
    /// <remarks>
    /// <code>
    /// Local-part      = Dot-string / Quoted-string
    /// Dot-string      = Atom *("."  Atom)
    /// Atom            = 1*atext
    /// Quoted-string   = DQUOTE *QcontentSMTP DQUOTE
    /// QcontentSMTP    = qtextSMTP / quoted-pairSMTP
    /// quoted-pairSMTP = %d92 %d32-126
    /// qtextSMTP       = %d32-33 / %d35-91 / %d93-126
    /// </code>
    /// The grammar has no comments: a parenthesis is not an <c>atext</c> character. There is no limit on the length
    /// of the local part, as in the other ports (#1029).
    /// </remarks>
    private static bool MatchEmailLocalPart(ReadOnlySpan<byte> segment)
    {
        return MatchEmailLocalPartCore(segment, allowNonAscii: false);
    }

    /// <summary>
    /// Validates an RFC 6531 <c>Local-part</c>, for the <c>idn-email</c> format.
    /// </summary>
    /// <param name="segment">The UTF-8 encoded local part (the text before the final <c>@</c>).</param>
    /// <returns><see langword="true"/> if the segment is a local part.</returns>
    /// <remarks>
    /// RFC 6531 extends the RFC 5321 grammar (see <see cref="MatchEmailLocalPart"/>) with
    /// <c>atext =/ UTF8-non-ascii</c> and <c>qtextSMTP =/ UTF8-non-ascii</c>: any character outside ASCII, whatever
    /// its category, is allowed in an atom or a quoted string. It does not extend <c>quoted-pairSMTP</c>.
    /// </remarks>
    private static bool MatchEmailLocalPartUnicode(ReadOnlySpan<byte> segment)
    {
        return MatchEmailLocalPartCore(segment, allowNonAscii: true);
    }

    private static bool MatchEmailLocalPartCore(ReadOnlySpan<byte> segment, bool allowNonAscii)
    {
        if (segment.Length == 0)
        {
            return false;
        }

        if (segment[0] == (byte)'"')
        {
            int i = 1;
            while (i < segment.Length)
            {
                byte c = segment[i];
                if (c == (byte)'"')
                {
                    // The closing quote ends the local part.
                    return i == segment.Length - 1;
                }

                if (c == (byte)'\\')
                {
                    // quoted-pairSMTP: a backslash and one printable ASCII character.
                    if (i + 1 >= segment.Length || segment[i + 1] < 32 || segment[i + 1] > 126)
                    {
                        return false;
                    }

                    i += 2;
                    continue;
                }

                if (c > 0x7F)
                {
                    if (!allowNonAscii)
                    {
                        return false;
                    }

                    i++;
                    continue;
                }

                // qtextSMTP: printable ASCII and space, except the quote and the backslash handled above.
                if (c < 32 || c > 126)
                {
                    return false;
                }

                i++;
            }

            // No closing quote.
            return false;
        }

        bool atomStart = true;
        for (int i = 0; i < segment.Length; i++)
        {
            byte c = segment[i];
            if (c == (byte)'.')
            {
                if (atomStart)
                {
                    // A dot at the start, or two dots in a row.
                    return false;
                }

                atomStart = true;
                continue;
            }

            if (c > 0x7F ? !allowNonAscii : AllowedLocalCharacters.IndexOf(c) < 0)
            {
                return false;
            }

            atomStart = false;
        }

        // A dot at the end leaves an empty atom.
        return !atomStart;
    }
}