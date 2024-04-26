// <copyright file="JsonTime.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.Internal;
using NodaTime;
using NodaTime.Text;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON time.
/// </summary>
public readonly partial struct JsonTime
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonTime"/> struct.
    /// </summary>
    /// <param name="value">The NodaTime time value.</param>
    public JsonTime(OffsetTime value)
    {
        this.jsonElementBacking = default;
        this.stringBacking = FormatTime(value);
        this.backing = Backing.String;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonTime"/> struct.
    /// </summary>
    /// <param name="value">The <see cref="TimeOnly"/> value.</param>
    public JsonTime(TimeOnly value)
        : this(new OffsetTime(LocalTime.FromTimeOnly(value), Offset.Zero))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonTime"/> struct.
    /// </summary>
    /// <param name="value">The <see cref="TimeOnly"/> value.</param>
    /// <param name="offset">A nodatime offset for the value.</param>
    public JsonTime(TimeOnly value, Offset offset)
        : this(new OffsetTime(LocalTime.FromTimeOnly(value), offset))
    {
    }
#endif

    /// <summary>
    /// Implicit conversion to OffsetTime.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator OffsetTime(JsonTime value)
    {
        return value.GetTime();
    }

    /// <summary>
    /// Implicit conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonTime(OffsetTime value)
    {
        return new JsonTime(value);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Implicit conversion to OffsetTime.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator TimeOnly(JsonTime value)
    {
        return new TimeOnly(value.GetTime().TickOfDay);
    }

    /// <summary>
    /// Implicit conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonTime(TimeOnly value)
    {
        return new JsonTime(value);
    }
#endif

    /// <summary>
    /// Gets the value as a OffsetTime.
    /// </summary>
    /// <returns>The value as a OffsetTime.</returns>
    /// <exception cref="InvalidOperationException">The value was not a time.</exception>
    public OffsetTime GetTime()
    {
        if (this.TryGetTime(out OffsetTime result))
        {
            return result;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Try to get the time value.
    /// </summary>
    /// <param name="result">The time value.</param>
    /// <returns><c>True</c> if it was possible to get a time value from the instance.</returns>
    public bool TryGetTime(out OffsetTime result)
    {
        if ((this.backing & Backing.String) != 0)
        {
            return TryParseTime(this.stringBacking, out result);
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            string? str = this.jsonElementBacking.GetString();
            if (str is not null)
            {
                return TryParseTime(str, out result);
            }
        }

        result = default;
        return false;
    }

    private static string FormatTime(OffsetTime value)
    {
        return OffsetTimePattern.ExtendedIso.Format(value);
    }

    private static bool TryParseTime(string text, out OffsetTime value)
    {
        ParseResult<OffsetTime> parseResult = OffsetTimePattern.ExtendedIso.Parse(text);

        // Aggravatingly, NodaTime rejects a lowercase Z to indicate a 0 offset, and its custom
        // pattern language doesn't seem to enable us to specify "either Z or z". It also
        // doesn't seem to be possible to produce a pattern that only accepts 'z' and which
        // otherwise reproduces the OffsetTimePattern.ExtendedIso behaviour, because that is
        // defined in terms of the "G" standard pattern, and you don't get to refer to a
        // standard pattern from inside a custom pattern.
        // https://nodatime.org/3.1.x/userguide/offset-patterns
        // It might be possible to use RegEx instead of NodeTime. But that's a relatively
        // complex alternative that requires some research.
        if (!parseResult.Success && text.Contains('z'))
        {
            text = text.Replace('z', 'Z');
            parseResult = OffsetTimePattern.ExtendedIso.Parse(text);
        }

        if (parseResult.Success)
        {
            value = parseResult.Value;

            // Despite what the documentation claims, NodaTime not only accepts 24:00:00
            // here, it is able to distinguish between this and 00:00:00 (whereas ISO
            // 8601 says they are to be treated as equivalent). However, the JSON Schema
            // test suite requires us to reject times of this form.
            return value.Hour != 24;
        }

        value = default;
        return false;
    }
}