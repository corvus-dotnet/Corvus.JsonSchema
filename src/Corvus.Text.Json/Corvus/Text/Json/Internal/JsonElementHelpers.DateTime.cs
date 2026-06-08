// <copyright file="JsonElementHelpers.DateTime.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NodaTime;
using NodaTime.Calendars;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Helper methods for JSON element date and time operations.
/// </summary>
public static partial class JsonElementHelpers
{
    /// <summary>
    /// Format a date as a UTF-8 string.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="bytesWritten">The number of bytes written to the output buffer.</param>
    /// <returns><see langword="true"/> if the date was formatted successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFormatLocalDate(in LocalDate value, Span<byte> output, out int bytesWritten)
    {
        return TryFormat(value, output, out bytesWritten);
    }

    /// <summary>
    /// Format a date as a UTF-8 string.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="bytesWritten">The number of bytes written to the output buffer.</param>
    /// <returns><see langword="true"/> if the date was formatted successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFormatOffsetDate(in OffsetDate value, Span<byte> output, out int bytesWritten)
    {
        return TryFormat(value, output, out bytesWritten);
    }

    /// <summary>
    /// Format an offset date time as a UTF-8 string.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="bytesWritten">The number of bytes written to the output buffer.</param>
    /// <returns><see langword="true"/> if the date was formatted successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFormatOffsetDateTime(in OffsetDateTime value, Span<byte> output, out int bytesWritten)
    {
        return TryFormat(value, output, out bytesWritten);
    }

    /// <summary>
    /// Format a time as a UTF-8 string.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="bytesWritten">The number of bytes written to the output buffer.</param>
    /// <returns><see langword="true"/> if the time was formatted successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFormatOffsetTime(in OffsetTime value, Span<byte> output, out int bytesWritten)
    {
        return TryFormat(value, output, out bytesWritten);
    }

    /// <summary>
    /// Format a period as a UTF-8 string for the <c>duration</c> format.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="bytesWritten">The number of bytes written to the output buffer.</param>
    /// <returns><see langword="true"/> if the period was formatted successfully.</returns>
    /// <remarks>
    /// The JSON Schema <c>duration</c> format (RFC 3339 Appendix A) does not permit fractional
    /// seconds, so any sub-second component of <paramref name="value"/> is rounded to the nearest
    /// whole second before formatting (rounding halves away from zero).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFormatPeriod(in Period value, Span<byte> output, out int bytesWritten)
    {
        return TryFormat(value, output, out bytesWritten);
    }

    /// <summary>
    /// Parse a period from a UTF-8 encoded string for the <c>duration</c> format.
    /// </summary>
    /// <param name="text">The UTF-8 encoded string to parse.</param>
    /// <returns>The resulting period.</returns>
    /// <exception cref="FormatException">Thrown when the text cannot be parsed as a valid period.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Period ParsePeriod(ReadOnlySpan<byte> text)
    {
        if (!TryParsePeriod(text, out Period value))
        {
            ThrowHelper.ThrowFormatException(DataType.Period);
        }

        return value;
    }

    /// <summary>
    /// Parse a period from a string for the <c>duration</c> format.
    /// </summary>
    /// <param name="text">The string to parse.</param>
    /// <param name="value">The resulting duration.</param>
    /// <returns><see langword="true"/> if the duration could be parsed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParsePeriod(ReadOnlySpan<byte> text, out Period value)
    {
        return Period.TryParse(text, out value);
    }

    /// <summary>
    /// Parse a local date from a UTF-8 encoded string for the <c>date</c> format.
    /// </summary>
    /// <param name="text">The UTF-8 encoded string to parse.</param>
    /// <returns>The resulting local date.</returns>
    /// <exception cref="FormatException">Thrown when the text cannot be parsed as a valid date.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalDate ParseLocalDate(ReadOnlySpan<byte> text)
    {
        if (!TryParseLocalDate(text, out LocalDate value))
        {
            ThrowHelper.ThrowFormatException(DataType.LocalDate);
        }

        return value;
    }

    /// <summary>
    /// Parse a date from a string for the <c>date</c> format.
    /// </summary>
    /// <param name="text">The string to parse.</param>
    /// <param name="value">The resulting date.</param>
    /// <returns><see langword="true"/> if the date could be parsed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseLocalDate(ReadOnlySpan<byte> text, out LocalDate value)
    {
        if (text.Length != 10)
        {
            value = default;
            return false;
        }

        if (!ParseDateCore(text, out int year, out int month, out int day) || !GregorianYearMonthDayCalculator.TryValidateGregorianYearMonthDay(year, month, day))
        {
            value = default;
            return false;
        }

        value = new LocalDate(year, month, day);
        return true;
    }

    /// <summary>
    /// Parse an offset time from a UTF-8 encoded string for the <c>time</c> format.
    /// </summary>
    /// <param name="text">The UTF-8 encoded string to parse.</param>
    /// <returns>The resulting offset time.</returns>
    /// <exception cref="FormatException">Thrown when the text cannot be parsed as a valid time.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OffsetTime ParseOffsetTime(ReadOnlySpan<byte> text)
    {
        if (!TryParseOffsetTime(text, out OffsetTime value))
        {
            ThrowHelper.ThrowFormatException(DataType.OffsetTime);
        }

        return value;
    }

    /// <summary>
    /// Parse a time from a string for the <c>time</c> format.
    /// </summary>
    /// <param name="text">The string to parse.</param>
    /// <param name="value">The resulting time.</param>
    /// <returns><see langword="true"/> if the time could be parsed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseOffsetTime(ReadOnlySpan<byte> text, out OffsetTime value)
    {
        if (text.Length < JsonConstants.MinimumTimeParseLength)
        {
            value = default;
            return false;
        }

        if (!ParseOffsetTimeCore(text, out int hours, out int minutes, out int seconds, out int milliseconds, out int microseconds, out int nanoseconds, out int offsetSeconds))
        {
            value = default;
            return false;
        }

        value = CreateOffsetTimeCore(hours, minutes, seconds, milliseconds, microseconds, nanoseconds, offsetSeconds);

        return true;
    }

    /// <summary>
    /// Creates an offset time from its individual components including nanosecond precision.
    /// </summary>
    /// <param name="hours">The hours component (0-23).</param>
    /// <param name="minutes">The minutes component (0-59).</param>
    /// <param name="seconds">The seconds component (0-59).</param>
    /// <param name="milliseconds">The milliseconds component (0-999).</param>
    /// <param name="microseconds">The microseconds component (0-999).</param>
    /// <param name="nanoseconds">The nanoseconds component (0-999).</param>
    /// <param name="offsetSeconds">The offset from UTC in seconds.</param>
    /// <returns>The constructed offset time.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OffsetTime CreateOffsetTimeCore(int hours, int minutes, int seconds, int milliseconds, int microseconds, int nanoseconds, int offsetSeconds)
    {
        OffsetTime value;
        var localTime = new LocalTime(hours, minutes, seconds, milliseconds);

        if (microseconds != 0 || nanoseconds != 0)
        {
            localTime = localTime.PlusNanoseconds((microseconds * 1000) + nanoseconds);
        }

        value = new OffsetTime(
            localTime,
            Offset.FromSeconds(offsetSeconds));
        return value;
    }

    /// <summary>
    /// Creates an offset time from its individual components with millisecond precision.
    /// </summary>
    /// <param name="hours">The hours component (0-23).</param>
    /// <param name="minutes">The minutes component (0-59).</param>
    /// <param name="seconds">The seconds component (0-59).</param>
    /// <param name="milliseconds">The milliseconds component (0-999).</param>
    /// <param name="offsetSeconds">The offset from UTC in seconds.</param>
    /// <returns>The constructed offset time.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OffsetTime CreateOffsetTimeCore(int hours, int minutes, int seconds, int milliseconds, int offsetSeconds)
    {
        OffsetTime value;
        var localTime = new LocalTime(hours, minutes, seconds, milliseconds);

        value = new OffsetTime(
            localTime,
            Offset.FromSeconds(offsetSeconds));
        return value;
    }

    /// <summary>
    /// Parse an offset date time from a UTF-8 encoded string for the <c>date-time</c> format.
    /// </summary>
    /// <param name="text">The UTF-8 encoded string to parse.</param>
    /// <returns>The resulting offset date time.</returns>
    /// <exception cref="FormatException">Thrown when the text cannot be parsed as a valid date time.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OffsetDateTime ParseOffsetDateTime(ReadOnlySpan<byte> text)
    {
        if (!TryParseOffsetDateTime(text, out OffsetDateTime value))
        {
            ThrowHelper.ThrowFormatException(DataType.OffsetTime);
        }

        return value;
    }

    /// <summary>
    /// Parse a date time from a string for the <c>date-time</c> format.
    /// </summary>
    /// <param name="text">The string to parse.</param>
    /// <param name="value">The resulting date time.</param>
    /// <returns><see langword="true"/> if the date could be parsed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseOffsetDateTime(ReadOnlySpan<byte> text, out OffsetDateTime value)
    {
        if (text.Length < JsonConstants.MinimumDateTimeOffsetParseLength)
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

        if (!ParseDateCore(text, out int year, out int month, out int day) || !GregorianYearMonthDayCalculator.TryValidateGregorianYearMonthDay(year, month, day))
        {
            value = default;
            return false;
        }

        if (!ParseOffsetTimeCore(text[11..], out int hours, out int minutes, out int seconds, out int milliseconds, out int microseconds, out int nanoseconds, out int offsetSeconds))
        {
            value = default;
            return false;
        }

        try
        {
            value = CreateOffsetDateTimeCore(year, month, day, hours, minutes, seconds, milliseconds, microseconds, nanoseconds, offsetSeconds);
        }
        catch (OverflowException)
        {
            // We cannot prevent NodaTime from throwing an OverflowException.
            value = default;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates an offset date time from its individual components including nanosecond precision.
    /// </summary>
    /// <param name="year">The year component.</param>
    /// <param name="month">The month component (1-12).</param>
    /// <param name="day">The day component (1-31).</param>
    /// <param name="hours">The hours component (0-23).</param>
    /// <param name="minutes">The minutes component (0-59).</param>
    /// <param name="seconds">The seconds component (0-59).</param>
    /// <param name="milliseconds">The milliseconds component (0-999).</param>
    /// <param name="microseconds">The microseconds component (0-999).</param>
    /// <param name="nanoseconds">The nanoseconds component (0-999).</param>
    /// <param name="offsetSeconds">The offset from UTC in seconds.</param>
    /// <returns>The constructed offset date time.</returns>
    public static OffsetDateTime CreateOffsetDateTimeCore(int year, int month, int day, int hours, int minutes, int seconds, int milliseconds, int microseconds, int nanoseconds, int offsetSeconds)
    {
        var value = new OffsetDateTime(
            new LocalDateTime(year, month, day, hours, minutes, seconds, milliseconds),
            Offset.FromSeconds(offsetSeconds));
        if (microseconds != 0 || nanoseconds != 0)
        {
            value = value.PlusNanoseconds((microseconds * 1000) + nanoseconds);
        }

        return value;
    }

    /// <summary>
    /// Creates an offset date time from its individual components with millisecond precision.
    /// </summary>
    /// <param name="year">The year component.</param>
    /// <param name="month">The month component (1-12).</param>
    /// <param name="day">The day component (1-31).</param>
    /// <param name="hours">The hours component (0-23).</param>
    /// <param name="minutes">The minutes component (0-59).</param>
    /// <param name="seconds">The seconds component (0-59).</param>
    /// <param name="milliseconds">The milliseconds component (0-999).</param>
    /// <param name="offsetSeconds">The offset from UTC in seconds.</param>
    /// <returns>The constructed offset date time.</returns>
    public static OffsetDateTime CreateOffsetDateTimeCore(int year, int month, int day, int hours, int minutes, int seconds, int milliseconds, int offsetSeconds)
    {
        var value = new OffsetDateTime(
            new LocalDateTime(year, month, day, hours, minutes, seconds, milliseconds),
            Offset.FromSeconds(offsetSeconds));

        return value;
    }

    /// <summary>
    /// Parse an offset date from a UTF-8 encoded string for the <c>date</c> format.
    /// </summary>
    /// <param name="text">The UTF-8 encoded string to parse.</param>
    /// <returns>The resulting offset date.</returns>
    /// <exception cref="FormatException">Thrown when the text cannot be parsed as a valid date.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OffsetDate ParseOffsetDate(ReadOnlySpan<byte> text)
    {
        if (!TryParseOffsetDate(text, out OffsetDate value))
        {
            ThrowHelper.ThrowFormatException(DataType.OffsetTime);
        }

        return value;
    }

    /// <summary>
    /// Parse a date time from a string for the <c>date-time</c> format.
    /// </summary>
    /// <param name="text">The string to parse.</param>
    /// <param name="value">The resulting date time.</param>
    /// <returns><see langword="true"/> if the date could be parsed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseOffsetDate(ReadOnlySpan<byte> text, out OffsetDate value)
    {
        if (text.Length < JsonConstants.MinimumDateParseLength)
        {
            value = default;
            return false;
        }

        if (!ParseDateCore(text, out int year, out int month, out int day) || !GregorianYearMonthDayCalculator.TryValidateGregorianYearMonthDay(year, month, day))
        {
            value = default;
            return false;
        }

        if (!ParseOffsetCore(text[10..], out int offsetSeconds))
        {
            value = default;
            return false;
        }

        value = new OffsetDate(
            new LocalDate(year, month, day),
            Offset.FromSeconds(offsetSeconds));

        return true;
    }

    // Roundtrippable format. One of
    // 012345678901234567890123456789012
    // ---------------------------------
    // 2017-06-12T05:30:45.7680000-07:00
    // 2017-06-12T05:30:45.7680000Z           (Z is short for "+00:00")
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool TryFormat(in OffsetDateTime dateTime, Span<byte> destination, out int bytesWritten)
    {
        const int bytesRequired = JsonConstants.MaximumFormatDateTimeOffsetLength;

        if (destination.Length < bytesRequired)
        {
            bytesWritten = 0;
            return false;
        }

        fixed (byte* dest = &MemoryMarshal.GetReference(destination))
        {
            Number.WriteFourDigits((uint)dateTime.Year, dest);
            dest[4] = (byte)'-';
            Number.WriteTwoDigits((uint)dateTime.Month, dest + 5);
            dest[7] = (byte)'-';
            Number.WriteTwoDigits((uint)dateTime.Day, dest + 8);
            dest[10] = (byte)'T';

            Number.WriteTwoDigits((uint)dateTime.Hour, dest + 11);
            dest[13] = (byte)':';
            Number.WriteTwoDigits((uint)dateTime.Minute, dest + 14);
            dest[16] = (byte)':';
            Number.WriteTwoDigits((uint)dateTime.Second, dest + 17);
            dest[19] = (byte)'.';
            Number.WriteDigits((uint)dateTime.TickOfSecond, dest + 20, 7);

            if (dateTime.Offset == Offset.Zero)
            {
                dest[27] = (byte)'Z';
                bytesWritten = 28;
            }
            else
            {
                int offsetTotalMinutes = (int)(dateTime.Offset.Ticks / TimeSpan.TicksPerMinute);

                char sign = '+';
                if (offsetTotalMinutes < 0)
                {
                    sign = '-';
                    offsetTotalMinutes = -offsetTotalMinutes;
                }

#if NET
                (int offsetHours, int offsetMinutes) = Math.DivRem(offsetTotalMinutes, 60);
#else
                int offsetHours = Math.DivRem(offsetTotalMinutes, 60, out int offsetMinutes);
#endif
                dest[27] = (byte)sign;
                Number.WriteTwoDigits((uint)offsetHours, dest + 28);
                dest[30] = (byte)':';
                Number.WriteTwoDigits((uint)offsetMinutes, dest + 31);
                bytesWritten = 33;
            }
        }

        return true;
    }

    // Roundtrippable format. One of
    // 012345678901234567890123456789012
    // ---------------------------------
    // 2017-06-12-07:00
    // 2017-06-12Z           (Z is short for "+00:00")
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool TryFormat(in OffsetDate date, Span<byte> destination, out int bytesWritten)
    {
        const int bytesRequired = JsonConstants.MaximumFormatOffsetDateLength;

        if (destination.Length < bytesRequired)
        {
            bytesWritten = 0;
            return false;
        }

        fixed (byte* dest = &MemoryMarshal.GetReference(destination))
        {
            Number.WriteFourDigits((uint)date.Year, dest);
            dest[4] = (byte)'-';
            Number.WriteTwoDigits((uint)date.Month, dest + 5);
            dest[7] = (byte)'-';
            Number.WriteTwoDigits((uint)date.Day, dest + 8);

            if (date.Offset == Offset.Zero)
            {
                dest[10] = (byte)'Z';
                bytesWritten = 11;
            }
            else
            {
                int offsetTotalMinutes = (int)(date.Offset.Ticks / TimeSpan.TicksPerMinute);

                char sign = '+';
                if (offsetTotalMinutes < 0)
                {
                    sign = '-';
                    offsetTotalMinutes = -offsetTotalMinutes;
                }

#if NET
                (int offsetHours, int offsetMinutes) = Math.DivRem(offsetTotalMinutes, 60);
#else
                int offsetHours = Math.DivRem(offsetTotalMinutes, 60, out int offsetMinutes);
#endif
                dest[10] = (byte)sign;
                Number.WriteTwoDigits((uint)offsetHours, dest + 11);
                dest[13] = (byte)':';
                Number.WriteTwoDigits((uint)offsetMinutes, dest + 14);
                bytesWritten = 16;
            }
        }

        return true;
    }

    // Roundtrippable format.
    // 0123456789
    // ----------
    // 2017-06-12
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool TryFormat(in LocalDate date, Span<byte> destination, out int bytesWritten)
    {
        const int bytesRequired = JsonConstants.MaximumFormatDateLength;

        if (destination.Length < bytesRequired)
        {
            bytesWritten = 0;
            return false;
        }

        bytesWritten = bytesRequired;

        fixed (byte* dest = &MemoryMarshal.GetReference(destination))
        {
            Number.WriteFourDigits((uint)date.Year, dest);
            dest[4] = (byte)'-';
            Number.WriteTwoDigits((uint)date.Month, dest + 5);
            dest[7] = (byte)'-';
            Number.WriteTwoDigits((uint)date.Day, dest + 8);
        }

        return true;
    }

    // Roundtrippable format. One of
    // 0123456789012345678901
    // ----------------------
    // 05:30:45.7680000-07:00
    // 05:30:45.7680000Z           (Z is short for "+00:00" but also distinguishes DateTimeKind.Utc from DateTimeKind.Local)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool TryFormat(in OffsetTime offsetTime, Span<byte> destination, out int bytesWritten)
    {
        const int bytesRequired = JsonConstants.MaximumFormatOffsetTimeLength;

        if (destination.Length < bytesRequired)
        {
            bytesWritten = 0;
            return false;
        }

        fixed (byte* dest = &MemoryMarshal.GetReference(destination))
        {
            Number.WriteTwoDigits((uint)offsetTime.Hour, dest);
            dest[2] = (byte)':';
            Number.WriteTwoDigits((uint)offsetTime.Minute, dest + 3);
            dest[5] = (byte)':';
            Number.WriteTwoDigits((uint)offsetTime.Second, dest + 6);
            dest[8] = (byte)'.';
            Number.WriteDigits((uint)offsetTime.TickOfSecond, dest + 9, 7);

            if (offsetTime.Offset == Offset.Zero)
            {
                dest[16] = (byte)'Z';
                bytesWritten = 17;
            }
            else
            {
                int offsetTotalMinutes = (int)(offsetTime.Offset.Ticks / TimeSpan.TicksPerMinute);

                char sign = '+';
                if (offsetTotalMinutes < 0)
                {
                    sign = '-';
                    offsetTotalMinutes = -offsetTotalMinutes;
                }

#if NET
                (int offsetHours, int offsetMinutes) = Math.DivRem(offsetTotalMinutes, 60);
#else
                int offsetHours = Math.DivRem(offsetTotalMinutes, 60, out int offsetMinutes);
#endif
                dest[16] = (byte)sign;
                Number.WriteTwoDigits((uint)offsetHours, dest + 17);
                dest[19] = (byte)':';
                Number.WriteTwoDigits((uint)offsetMinutes, dest + 20);
                bytesWritten = 22;
            }
        }

        return true;
    }

    // Round-trippable RFC 3339 Appendix A duration. The grammar permits only integer values for each
    // unit, and seconds is the smallest unit (no fractional seconds), so any sub-second component is
    // rounded to the nearest whole second by RoundedToWholeSeconds before formatting. One of:
    // 01234567890123456789012345678901234567890123456789012345678901234567890123456789
    // --------------------------------------------------------------------------------
    // P2147483647Y2147483647M2147483647W2147483647DT2147483647H2147483647M-2147483647S
    // P2147483647Y2147483647M2147483647W2147483647D
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryFormat(in Period incomingPeriod, Span<byte> destination, out int bytesWritten)
    {
        const int bytesRequired = JsonConstants.MaximumFormatPeriodLength;

        if (destination.Length < bytesRequired)
        {
            bytesWritten = 0;
            return false;
        }

        Period period = incomingPeriod.RoundedToWholeSeconds();

        if (period.Equals(Period.Zero))
        {
            "P0D"u8.CopyTo(destination);
            bytesWritten = 3;
            return true;
        }

        int index = 0;

        destination[index++] = (byte)'P';

        // After RoundedToWholeSeconds the period is normalized: weeks and ticks are zero and the
        // sub-second component has been folded into seconds, so the period reduces to Y/M/D and H/M/S.
        // RFC 3339 Appendix A allows zero-valued units to be omitted, but the grammar is contiguous:
        //   dur-year = Y [dur-month], dur-month = M [dur-day]      (a Day after a Year needs the Month)
        //   dur-hour = H [dur-minute], dur-minute = M [dur-second] (a Second after an Hour needs the Minute)
        // so a zero "bridge" unit is retained when omitting it would create an invalid gap.
        bool hasYears = period.Years != 0;
        bool hasDays = period.Days != 0;

        if (hasYears && !TryAppendUnit(period.Years, (byte)'Y', destination, ref index))
        {
            bytesWritten = 0;
            return false;
        }

        // Emit months when non-zero, or as the bridge between years and days.
        if ((period.Months != 0 || (hasYears && hasDays)) && !TryAppendUnit(period.Months, (byte)'M', destination, ref index))
        {
            bytesWritten = 0;
            return false;
        }

        if (hasDays && !TryAppendUnit(period.Days, (byte)'D', destination, ref index))
        {
            bytesWritten = 0;
            return false;
        }

        if (period.HasTimeComponent)
        {
            destination[index++] = (byte)'T';

            bool hasHours = period.Hours != 0;

            // The sub-second component has been rounded into seconds, so seconds is the smallest unit.
            bool hasSeconds = period.Seconds != 0;

            if (hasHours && !TryAppendUnit(period.Hours, (byte)'H', destination, ref index))
            {
                bytesWritten = 0;
                return false;
            }

            // Emit minutes when non-zero, or as the bridge between hours and seconds.
            if ((period.Minutes != 0 || (hasHours && hasSeconds)) && !TryAppendUnit(period.Minutes, (byte)'M', destination, ref index))
            {
                bytesWritten = 0;
                return false;
            }

            if (hasSeconds && !TryAppendUnit(period.Seconds, (byte)'S', destination, ref index))
            {
                bytesWritten = 0;
                return false;
            }
        }

        bytesWritten = index;
        return true;
    }

    /// <summary>
    /// Appends a single duration unit (an integer value followed by its designator) to the destination.
    /// </summary>
    /// <param name="value">The integer value of the unit. Negative values are formatted with a leading '-'.</param>
    /// <param name="designator">The RFC 3339 designator byte for the unit (for example <c>(byte)'S'</c>).</param>
    /// <param name="destination">The output buffer.</param>
    /// <param name="index">The current write position, advanced past the bytes written.</param>
    /// <returns><see langword="true"/> if the unit was written successfully.</returns>
    private static bool TryAppendUnit(long value, byte designator, Span<byte> destination, ref int index)
    {
        if (!Utf8Formatter.TryFormat(value, destination.Slice(index), out int localWritten))
        {
            return false;
        }

        index += localWritten;
        destination[index++] = designator;
        return true;
    }
}