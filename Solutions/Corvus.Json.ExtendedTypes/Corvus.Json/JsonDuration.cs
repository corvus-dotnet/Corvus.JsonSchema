// <copyright file="JsonDuration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.Internal;
using NodaTime;
using NodaTime.Text;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON duration.
/// </summary>
public readonly partial struct JsonDuration : IJsonString<JsonDuration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDuration"/> struct.
    /// </summary>
    /// <param name="value">The NodaTime Period value.</param>
    public JsonDuration(Period value)
    {
        this.jsonElementBacking = default;
        this.stringBacking = FormatPeriod(value);
        this.backing = Backing.String;
    }

    /// <summary>
    /// Implicit conversion to Period.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a duration.</exception>
    public static implicit operator Period(JsonDuration value)
    {
        return value.GetPeriod();
    }

    /// <summary>
    /// Implicit conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonDuration(Period value)
    {
        return new JsonDuration(value);
    }

    /// <summary>
    /// Gets the value as a Period.
    /// </summary>
    /// <returns>The value as a Period.</returns>
    /// <exception cref="InvalidOperationException">The value was not a duration.</exception>
    public Period GetPeriod()
    {
        if (this.TryGetPeriod(out Period result))
        {
            return result;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Try to get the value as Perion.
    /// </summary>
    /// <param name="result">The Period.</param>
    /// <returns><c>True</c> if it was possible to get a Period value from the instance.</returns>
    public bool TryGetPeriod(out Period result)
    {
        if ((this.backing & Backing.String) != 0)
        {
            return TryParsePeriod(this.stringBacking, out result);
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            string? str = this.jsonElementBacking.GetString();
            return TryParsePeriod(str!, out result);
        }

        result = Period.Zero;
        return false;
    }

    private static string FormatPeriod(Period value)
    {
        return PeriodPattern.NormalizingIso.Format(value);
    }

    private static bool TryParsePeriod(string text, out Period value)
    {
        string tupper = text.ToUpperInvariant();
        ParseResult<Period> parseResult = PeriodPattern.NormalizingIso.Parse(tupper);
        if (parseResult.Success)
        {
            value = parseResult.Value;
            return true;
        }

        value = Period.Zero;
        return false;
    }
}