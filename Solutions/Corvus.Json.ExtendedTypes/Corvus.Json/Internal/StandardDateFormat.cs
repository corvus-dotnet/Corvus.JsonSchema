// <copyright file="StandardDateFormat.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using NodaTime;
using NodaTime.Calendars;
using NodaTime.Text;

namespace Corvus.Json.Internal;

/// <summary>
/// Standard date format parsing and formatting.
/// </summary>
public static class StandardDateFormat
{
    /// <summary>
    /// Convert a date to a string for the <c>date</c> format.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The ISO formatted date as a string.</returns>
    public static string FormatDate(in LocalDate value)
    {
        return LocalDatePattern.Iso.Format(value);
    }

    /// <summary>
    /// Convert a date time to a string for the <c>date-time</c> format.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The ISO formatted date-time as a string.</returns>
    public static string FormatDateTime(in OffsetDateTime value)
    {
        return OffsetDateTimePattern.ExtendedIso.Format(value);
    }

    /// <summary>
    /// Convert a time to a string for the <c>time</c> format.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The ISO formatted time as a string.</returns>
    public static string FormatTime(OffsetTime value)
    {
        return OffsetTimePattern.ExtendedIso.Format(value);
    }

    /// <summary>
    /// Convert a period to a string for the <c>duration</c> format.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The ISO formatted duration as a string.</returns>
    public static string FormatPeriod(Period value)
    {
        return value.ToString();
    }

    /// <summary>
    /// Convert a period to a string for the <c>duration</c> format.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The ISO formatted duration as a string.</returns>
    public static string FormatPeriod(NodaTime.Period value)
    {
        return PeriodPattern.NormalizingIso.Format(value);
    }

    /// <summary>
    /// Parse a date from a string for the <c>date</c> format.
    /// </summary>
    /// <param name="text">The string to parse.</param>
    /// <param name="state">The (unused) state.</param>
    /// <param name="value">The resulting date.</param>
    /// <returns><see langword="true"/> if the date could be parsed.</returns>
    public static bool DateParser(ReadOnlySpan<char> text, in object? state, out LocalDate value)
    {
        if (text.Length != 10)
        {
            value = default;
            return false;
        }

        if (!ParseDateCore(text, out int year, out int month, out int day))
        {
            value = default;
            return false;
        }

        value = new LocalDate(year, month, day);
        return true;
    }

    /// <summary>
    /// Parse a time from a string for the <c>time</c> format.
    /// </summary>
    /// <param name="text">The string to parse.</param>
    /// <param name="state">The (unused) state.</param>
    /// <param name="value">The resulting time.</param>
    /// <returns><see langword="true"/> if the time could be parsed.</returns>
    public static bool TimeParser(ReadOnlySpan<char> text, in object? state, out OffsetTime value)
    {
        if (text.Length < 9)
        {
            value = default;
            return false;
        }

        if (!ParseOffsetTimeCore(text, out int hours, out int minutes, out int seconds, out int milliseconds, out int nanoseconds, out int offsetSeconds))
        {
            value = default;
            return false;
        }

        var localTime = new LocalTime(hours, minutes, seconds, milliseconds);

        if (nanoseconds != 0)
        {
            localTime = localTime.PlusNanoseconds(nanoseconds);
        }

        value = new OffsetTime(
            localTime,
            Offset.FromSeconds(offsetSeconds));

        return true;
    }

    /// <summary>
    /// Parse a date time from a string for the <c>date-time</c> format.
    /// </summary>
    /// <param name="text">The string to parse.</param>
    /// <param name="state">The (unused) state.</param>
    /// <param name="value">The resulting date time.</param>
    /// <returns><see langword="true"/> if the date could be parsed.</returns>
    public static bool DateTimeParser(ReadOnlySpan<char> text, in object? state, out OffsetDateTime value)
    {
        if (text.Length < 19)
        {
            value = default;
            return false;
        }

        // We allow lower case T in the middle which is not strictly
        // permissible, but tested for by the standard json-schema test suite.
        if (text[10] != 'T' && text[10] != 't')
        {
            value = default;
            return false;
        }

        if (!ParseDateCore(text, out int year, out int month, out int day))
        {
            value = default;
            return false;
        }

        if (!ParseOffsetTimeCore(text[11..], out int hours, out int minutes, out int seconds, out int milliseconds, out int nanoseconds, out int offsetSeconds))
        {
            value = default;
            return false;
        }

        value = new OffsetDateTime(
            new LocalDateTime(year, month, day, hours, minutes, seconds, milliseconds),
            Offset.FromSeconds(offsetSeconds));

        if (nanoseconds != 0)
        {
            value = value.PlusNanoseconds(nanoseconds);
        }

        return true;
    }

    private static bool ParseDateCore(ReadOnlySpan<char> text, out int year, out int month, out int day)
    {
        if (text[4] != '-' ||
            text[7] != '-' ||
            IsNotNumeric(text[0]) ||
            IsNotNumeric(text[1]) ||
            IsNotNumeric(text[2]) ||
            IsNotNumeric(text[3]) ||
            IsNotNumeric(text[5]) ||
            IsNotNumeric(text[6]) ||
            IsNotNumeric(text[8]) ||
            IsNotNumeric(text[9]))
        {
            year = 0;
            month = 0;
            day = 0;
            return false;
        }

        year = ((text[0] - '0') * 1000) + ((text[1] - '0') * 100) + ((text[2] - '0') * 10) + (text[3] - '0');
        month = ((text[5] - '0') * 10) + (text[6] - '0');
        day = ((text[8] - '0') * 10) + (text[9] - '0');
        return GregorianYearMonthDayCalculator.TryValidateGregorianYearMonthDay(year, month, day);
    }

    private static bool ParseOffsetTimeCore(ReadOnlySpan<char> text, out int hours, out int minutes, out int seconds, out int milliseconds, out int nanoseconds, out int offsetSeconds)
    {
        // We can start searching from 8 after the beginning as we know it won't come in
        // the time section
        int indexOfOffset = text[8..].IndexOfAny("zZ+-".AsSpan());

        if (indexOfOffset < 0)
        {
            hours = 0;
            minutes = 0;
            seconds = 0;
            milliseconds = 0;
            offsetSeconds = 0;
            nanoseconds = 0;
            return false;
        }

        // Add the 8 characters back on!
        indexOfOffset += 8;

        if (!ParseTimeCore(text[..indexOfOffset], out hours, out minutes, out seconds, out milliseconds, out nanoseconds))
        {
            offsetSeconds = 0;
            return false;
        }

        if (text[indexOfOffset] == 'Z' || text[indexOfOffset] == 'z')
        {
            offsetSeconds = 0;
            if (text.Length == indexOfOffset + 1)
            {
                return true;
            }

            hours = 0;
            minutes = 0;
            seconds = 0;
            milliseconds = 0;
            offsetSeconds = 0;
            nanoseconds = 0;
            return false;
        }

        ReadOnlySpan<char> offsetText = text[(indexOfOffset + 1)..];
        if (offsetText.Length != 5 ||
            offsetText[2] != ':' ||
            IsNotNumeric(offsetText[0]) ||
            IsNotNumeric(offsetText[1]) ||
            IsNotNumeric(offsetText[3]) ||
            IsNotNumeric(offsetText[4]))
        {
            hours = 0;
            minutes = 0;
            seconds = 0;
            milliseconds = 0;
            offsetSeconds = 0;
            return false;
        }

        int offsetHours = ((offsetText[0] - '0') * 10) + (offsetText[1] - '0');
        int offsetMinutes = ((offsetText[3] - '0') * 10) + (offsetText[4] - '0');

        if (offsetHours > 18 || offsetMinutes >= 60)
        {
            hours = 0;
            minutes = 0;
            seconds = 0;
            milliseconds = 0;
            offsetSeconds = 0;
            return false;
        }

        offsetSeconds = (offsetHours * 3600) + (offsetMinutes * 60);

        // You can't have an offset more than +/- 18 hours
        if (offsetSeconds > 64800)
        {
            hours = 0;
            minutes = 0;
            seconds = 0;
            milliseconds = 0;
            offsetSeconds = 0;
            return false;
        }

        if (text[indexOfOffset] == '-')
        {
            offsetSeconds *= -1;
        }

        return true;
    }

    private static bool ParseTimeCore(ReadOnlySpan<char> text, out int hours, out int minutes, out int seconds, out int milliseconds, out int nanoseconds)
    {
        if (text[2] != ':' ||
            text[5] != ':' ||
            IsNotNumeric(text[0]) ||
            IsNotNumeric(text[1]) ||
            IsNotNumeric(text[3]) ||
            IsNotNumeric(text[4]) ||
            IsNotNumeric(text[6]) ||
            IsNotNumeric(text[7]))
        {
            hours = 0;
            minutes = 0;
            seconds = 0;
            milliseconds = 0;
            nanoseconds = 0;
            return false;
        }

        hours = ((text[0] - '0') * 10) + (text[1] - '0');
        minutes = ((text[3] - '0') * 10) + (text[4] - '0');
        seconds = ((text[6] - '0') * 10) + (text[7] - '0');
        milliseconds = 0;

        if (text.Length > 8)
        {
            // There must be a dot for milliseconds, and
            // there must be at least 1 millisecond digit
            if (text[8] != '.' || text.Length <= 9)
            {
                hours = 0;
                minutes = 0;
                seconds = 0;
                milliseconds = 0;
                nanoseconds = 0;
                return false;
            }

            // The initial multiplier is 10^(number of digits remaining - 1)
            int multiplier = (int)Math.Pow(10, text.Length - 10);

            // This does the milliseconds
            for (int i = 9; i < Math.Min(12, text.Length); ++i)
            {
                if (IsNotNumeric(text[i]))
                {
                    hours = 0;
                    minutes = 0;
                    seconds = 0;
                    milliseconds = 0;
                    nanoseconds = 0;
                    return false;
                }

                milliseconds += (text[i] - '0') * multiplier;
                multiplier /= 10;
            }
        }

        if (milliseconds >= 1000)
        {
            nanoseconds = milliseconds % 1000;
            milliseconds /= 1000;
        }
        else
        {
            nanoseconds = 0;
        }

        if (hours < 24 && minutes < 60 && seconds < 60 && milliseconds < 1000 && nanoseconds < 1000)
        {
            return true;
        }

        hours = 0;
        minutes = 0;
        seconds = 0;
        milliseconds = 0;
        nanoseconds = 0;
        return false;
    }

    private static bool IsNotNumeric(char v)
    {
        return v < '0' || v > '9';
    }
}