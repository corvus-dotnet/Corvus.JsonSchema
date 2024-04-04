// <copyright file="JsonDate.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.Internal;
using NodaTime;
using NodaTime.Calendars;
using NodaTime.Text;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON date.
/// </summary>
public readonly partial struct JsonDate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDate"/> struct.
    /// </summary>
    /// <param name="value">The NodaTime LocalDate value.</param>
    public JsonDate(in LocalDate value)
    {
        this.jsonElementBacking = default;
        this.stringBacking = FormatDate(value);
        this.backing = Backing.String;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDate"/> struct.
    /// </summary>
    /// <param name="value">The date time from which to construct the date.</param>
#if NET8_0_OR_GREATER
    public JsonDate(in DateTime value)
#else
    public JsonDate(DateTime value)
#endif
    {
        this.jsonElementBacking = default;
        this.stringBacking = FormatDate(LocalDate.FromDateTime(value));
        this.backing = Backing.String;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDate"/> struct.
    /// </summary>
    /// <param name="value">The date time from which to construct the date.</param>
    /// <param name="calendar">The calendar system with which to interpret the date.</param>
#if NET8_0_OR_GREATER
    public JsonDate(in DateTime value, CalendarSystem calendar)
#else
    public JsonDate(DateTime value, CalendarSystem calendar)
#endif
    {
        this.jsonElementBacking = default;
        this.stringBacking = FormatDate(LocalDate.FromDateTime(value, calendar));
        this.backing = Backing.String;
    }

    /// <summary>
    /// Implicit conversion to LocalDate.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator LocalDate(in JsonDate value)
    {
        return value.GetDate();
    }

    /// <summary>
    /// Implicit conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonDate(in LocalDate value)
    {
        return new JsonDate(value);
    }

    /// <summary>
    /// Gets the value as a LocalDate.
    /// </summary>
    /// <returns>The value as a LocalDate.</returns>
    /// <exception cref="InvalidOperationException">The value was not a date.</exception>
    public LocalDate GetDate()
    {
        if (this.TryGetDate(out LocalDate result))
        {
            return result;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Try to get the date value.
    /// </summary>
    /// <param name="result">The date value.</param>
    /// <returns><c>True</c> if it was possible to get a date value from the instance.</returns>
    public bool TryGetDate(out LocalDate result)
    {
        if ((this.backing & Backing.String) != 0)
        {
            return DateParser(this.stringBacking, default, out result);
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
#if NET8_0_OR_GREATER
            return this.jsonElementBacking.TryGetValue(DateParser, default(object?), out result);
#else
            return DateParser(this.jsonElementBacking.GetString(), default, out result);
#endif
        }

        result = default;
        return false;
    }

    private static string FormatDate(in LocalDate value)
    {
        return LocalDatePattern.Iso.Format(value);
    }

#if NET8_0_OR_GREATER
    private static bool DateParser(ReadOnlySpan<char> text, in object? state, out LocalDate value)
#else
    private static bool DateParser(string text, in object? state, out LocalDate value)
#endif
    {
        if (text.Length != 10 ||
            text[4] != '-' ||
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
            value = default;
            return false;
        }

        int year = ((text[0] - '0') * 1000) + ((text[1] - '0') * 100) + ((text[2] - '0') * 10) + (text[3] - '0');
        int month = ((text[5] - '0') * 10) + (text[6] - '0');
        int day = ((text[8] - '0') * 10) + (text[9] - '0');

        if (!GregorianYearMonthDayCalculator.TryValidateGregorianYearMonthDay(year, month, day))
        {
            value = default;
            return false;
        }

        value = new LocalDate(year, month, day);
        return true;
    }

    private static bool IsNotNumeric(char v)
    {
        return v < '0' || v > '9';
    }
}