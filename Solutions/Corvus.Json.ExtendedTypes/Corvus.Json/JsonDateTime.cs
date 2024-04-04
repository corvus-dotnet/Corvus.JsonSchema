// <copyright file="JsonDateTime.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.Internal;
using NodaTime;
using NodaTime.Text;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON dateTime.
/// </summary>
public readonly partial struct JsonDateTime
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDateTime"/> struct.
    /// </summary>
    /// <param name="value">The NodaTime OffsetDateTime value.</param>
    public JsonDateTime(in OffsetDateTime value)
    {
        this.jsonElementBacking = default;
        this.stringBacking = FormatDateTime(value);
        this.backing = Backing.String;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDateTime"/> struct.
    /// </summary>
    /// <param name="value">The date time offset from which to construct the date.</param>
#if NET8_0_OR_GREATER
    public JsonDateTime(in DateTimeOffset value)
#else
    public JsonDateTime(DateTimeOffset value)
#endif
    {
        this.jsonElementBacking = default;
        this.stringBacking = FormatDateTime(OffsetDateTime.FromDateTimeOffset(value));
        this.backing = Backing.String;
    }

    /// <summary>
    /// Conversion from OffsetDateTime.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonDateTime(in OffsetDateTime value)
    {
        return new JsonDateTime(value);
    }

    /// <summary>
    /// Conversion to OffsetDateTime.
    /// </summary>
    /// <param name="value">The number from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a datetime.</exception>
    public static implicit operator OffsetDateTime(in JsonDateTime value)
    {
        return value.GetDateTime();
    }

    /// <summary>
    /// Gets the value as a OffsetDateTime.
    /// </summary>
    /// <returns>The value as a OffsetDateTime.</returns>
    /// <exception cref="InvalidOperationException">The value was not a datetime.</exception>
    public OffsetDateTime GetDateTime()
    {
        if (this.TryGetDateTime(out OffsetDateTime result))
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
    public bool TryGetDateTime(out OffsetDateTime result)
    {
        if ((this.backing & Backing.String) != 0)
        {
            return TryParseDateTime(this.stringBacking, out result);
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            string? str = this.jsonElementBacking.GetString();
            if (str is not null)
            {
                return TryParseDateTime(str!, out result);
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <typeparam name="T">The type of the item with which to compare.</typeparam>
    /// <param name="other">The item with which to compare.</param>
    /// <returns><c>True</c> if the items are equal.</returns>
    public bool Equals<T>(in T other)
        where T : struct, IJsonValue<T>
    {
        if (this.IsNull() && other.IsNull())
        {
            return true;
        }

        if (other.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (!TryParseDateTime((string)other.AsString, out OffsetDateTime otherDate))
        {
            return false;
        }

        if (!this.TryGetDateTime(out OffsetDateTime thisDate))
        {
            return false;
        }

        return thisDate.Equals(otherDate);
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="other">The item with which to compare.</param>
    /// <returns><c>True</c> if the items are equal.</returns>
    public bool Equals(in JsonDateTime other)
    {
        if (this.IsNull() && other.IsNull())
        {
            return true;
        }

        JsonValueKind valueKind = this.ValueKind;
        if (valueKind != JsonValueKind.String || other.ValueKind != valueKind)
        {
            return false;
        }

        if (!this.TryGetDateTime(out OffsetDateTime thisDate))
        {
            return false;
        }

        if (!other.TryGetDateTime(out OffsetDateTime otherDate))
        {
            return false;
        }

        return thisDate.Equals(otherDate);
    }

    private static string FormatDateTime(in OffsetDateTime value)
    {
        return OffsetDateTimePattern.ExtendedIso.Format(value);
    }

    private static bool TryParseDateTime(string text, out OffsetDateTime value)
    {
        string tupper = text.ToUpperInvariant();
        ParseResult<OffsetDateTime> parseResult = OffsetDateTimePattern.ExtendedIso.Parse(tupper);
        if (parseResult.Success)
        {
            value = parseResult.Value;
            return true;
        }

        value = default;
        return false;
    }
}