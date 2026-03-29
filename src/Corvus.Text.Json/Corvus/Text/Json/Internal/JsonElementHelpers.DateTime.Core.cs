// <copyright file="JsonElementHelpers.DateTime.Core.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
#if CORVUS_TEXT_JSON_CODEGENERATION

namespace Corvus.Text.Json.CodeGeneration.Internal;
#else

namespace Corvus.Text.Json.Internal;
#endif

/// <summary>
/// Core helper methods for parsing date and time components from ISO 8601 formatted strings.
/// </summary>
public static partial class JsonElementHelpers
{
    /// <summary>
    /// Parses a date string in ISO 8601 format (YYYY-MM-DD) and extracts the year, month, and day components.
    /// </summary>
    /// <param name="text">The UTF-8 encoded text containing the date string to parse.</param>
    /// <param name="year">When this method returns, contains the year component of the date.</param>
    /// <param name="month">When this method returns, contains the month component of the date (1-12).</param>
    /// <param name="day">When this method returns, contains the day component of the date (1-31).</param>
    /// <returns><see langword="true"/> if the date was successfully parsed; otherwise, <see langword="false"/>.</returns>
    public static bool ParseDateCore(ReadOnlySpan<byte> text, out int year, out int month, out int day)
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
        return true;
    }

    /// <summary>
    /// Parses a time string with optional offset in ISO 8601 format and extracts the time and offset components.
    /// </summary>
    /// <param name="text">The UTF-8 encoded text containing the time string to parse.</param>
    /// <param name="hours">When this method returns, contains the hours component of the time (0-23).</param>
    /// <param name="minutes">When this method returns, contains the minutes component of the time (0-59).</param>
    /// <param name="seconds">When this method returns, contains the seconds component of the time (0-59).</param>
    /// <param name="milliseconds">When this method returns, contains the milliseconds component of the time (0-999).</param>
    /// <param name="microseconds">When this method returns, contains the microseconds component of the time (0-999).</param>
    /// <param name="nanoseconds">When this method returns, contains the nanoseconds component of the time (0-999).</param>
    /// <param name="offsetSeconds">When this method returns, contains the timezone offset in seconds from UTC.</param>
    /// <returns><see langword="true"/> if the time was successfully parsed; otherwise, <see langword="false"/>.</returns>
    public static bool ParseOffsetTimeCore(ReadOnlySpan<byte> text, out int hours, out int minutes, out int seconds, out int milliseconds, out int microseconds, out int nanoseconds, out int offsetSeconds)
    {
        // We can start searching from 8 after the beginning as we know it won't come in
        // the time section
        int indexOfOffset = text[8..].IndexOfAny("zZ+-"u8);

        if (indexOfOffset < 0)
        {
            hours = 0;
            minutes = 0;
            seconds = 0;
            milliseconds = 0;
            microseconds = 0;
            nanoseconds = 0;
            offsetSeconds = 0;
            return false;
        }

        // Add the 8 characters back on!
        indexOfOffset += 8;

        if (!ParseTimeCore(text[..indexOfOffset], out hours, out minutes, out seconds, out milliseconds, out microseconds, out nanoseconds))
        {
            offsetSeconds = 0;
            return false;
        }

        if (!ParseOffsetCore(text[indexOfOffset..], out offsetSeconds))
        {
            hours = 0;
            minutes = 0;
            seconds = 0;
            milliseconds = 0;
            microseconds = 0;
            nanoseconds = 0;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Parses a timezone offset string in ISO 8601 format (±HH:MM or Z) and extracts the offset in seconds.
    /// </summary>
    /// <param name="text">The UTF-8 encoded text containing the offset string to parse.</param>
    /// <param name="offsetSeconds">When this method returns, contains the timezone offset in seconds from UTC.</param>
    /// <returns><see langword="true"/> if the offset was successfully parsed; otherwise, <see langword="false"/>.</returns>
    public static bool ParseOffsetCore(ReadOnlySpan<byte> text, out int offsetSeconds)
    {
        if (text[0] == 'Z' || text[0] == 'z')
        {
            offsetSeconds = 0;
            return text.Length == 1;
        }

        ReadOnlySpan<byte> offsetText = text[1..];

        if (!(offsetText.Length == 2 || offsetText.Length == 5) ||
            IsNotNumeric(offsetText[0]) ||
            IsNotNumeric(offsetText[1]) ||
            (offsetText.Length == 5 &&
             offsetText[2] != ':'))
        {
            offsetSeconds = 0;
            return false;
        }

        int offsetHours = ((offsetText[0] - '0') * 10) + (offsetText[1] - '0');
        int offsetMinutes = offsetText.Length == 5 ? ((offsetText[3] - '0') * 10) + (offsetText[4] - '0') : 0;

        if (offsetHours > 18 || offsetMinutes >= 60)
        {
            offsetSeconds = 0;
            return false;
        }

        offsetSeconds = (offsetHours * 3600) + (offsetMinutes * 60);

        // You can't have an offset more than +/- 14 hours
        if (offsetSeconds > 50400)
        {
            offsetSeconds = 0;
            return false;
        }

        if (text[0] == '-')
        {
            offsetSeconds *= -1;
        }

        return true;
    }

    /// <summary>
    /// Parses a time string in ISO 8601 format (HH:MM:SS[.nnnnnnnnn]) and extracts the time components.
    /// </summary>
    /// <param name="text">The UTF-8 encoded text containing the time string to parse.</param>
    /// <param name="hours">When this method returns, contains the hours component of the time (0-23).</param>
    /// <param name="minutes">When this method returns, contains the minutes component of the time (0-59).</param>
    /// <param name="seconds">When this method returns, contains the seconds component of the time (0-59).</param>
    /// <param name="milliseconds">When this method returns, contains the milliseconds component of the time (0-999).</param>
    /// <param name="microseconds">When this method returns, contains the microseconds component of the time (0-999).</param>
    /// <param name="nanoseconds">When this method returns, contains the nanoseconds component of the time (0-999).</param>
    /// <returns><see langword="true"/> if the time was successfully parsed; otherwise, <see langword="false"/>.</returns>
    public static bool ParseTimeCore(ReadOnlySpan<byte> text, out int hours, out int minutes, out int seconds, out int milliseconds, out int microseconds, out int nanoseconds)
    {
        if (text.Length > 18 ||
            text[2] != ':' ||
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
            microseconds = 0;
            nanoseconds = 0;
            return false;
        }

        hours = ((text[0] - '0') * 10) + (text[1] - '0');
        minutes = ((text[3] - '0') * 10) + (text[4] - '0');
        seconds = ((text[6] - '0') * 10) + (text[7] - '0');
        milliseconds = 0;
        microseconds = 0;
        nanoseconds = 0;

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
                microseconds = 0;
                nanoseconds = 0;
                return false;
            }

            // This does the milliseconds
            // The initial multiplier is 10^(number of digits remaining - 1)
            int multiplier = 100;
            for (int i = 9; i < Math.Min(12, text.Length); ++i)
            {
                if (IsNotNumeric(text[i]))
                {
                    hours = 0;
                    minutes = 0;
                    seconds = 0;
                    milliseconds = 0;
                    microseconds = 0;
                    nanoseconds = 0;
                    return false;
                }

                milliseconds += (text[i] - '0') * multiplier;
                multiplier /= 10;
            }

            // This does the microseconds
            multiplier = 100;
            for (int i = 12; i < Math.Min(15, text.Length); ++i)
            {
                if (IsNotNumeric(text[i]))
                {
                    hours = 0;
                    minutes = 0;
                    seconds = 0;
                    milliseconds = 0;
                    microseconds = 0;
                    nanoseconds = 0;
                    return false;
                }

                microseconds += (text[i] - '0') * multiplier;
                multiplier /= 10;
            }

            // This does the nanoseconds
            multiplier = 100;
            for (int i = 15; i < Math.Min(18, text.Length); ++i)
            {
                if (IsNotNumeric(text[i]))
                {
                    hours = 0;
                    minutes = 0;
                    seconds = 0;
                    milliseconds = 0;
                    microseconds = 0;
                    nanoseconds = 0;
                    return false;
                }

                nanoseconds += (text[i] - '0') * multiplier;
                multiplier /= 10;
            }
        }

        if (hours < 24 && minutes < 60 && seconds < 60 && milliseconds < 1000 && microseconds < 1000 && nanoseconds < 1000)
        {
            return true;
        }

        hours = 0;
        minutes = 0;
        seconds = 0;
        milliseconds = 0;
        microseconds = 0;
        nanoseconds = 0;
        return false;
    }

    private static bool IsNotNumeric(byte v)
    {
        return v < '0' || v > '9';
    }
}