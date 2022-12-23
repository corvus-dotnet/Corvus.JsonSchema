// <copyright file="JsonDuration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Corvus.Json.Internal;

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
    /// Initializes a new instance of the <see cref="JsonDuration"/> struct.
    /// </summary>
    /// <param name="value">The NodaTime Period value.</param>
    public JsonDuration(NodaTime.Period value)
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
    /// Implicit conversion to Period.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a duration.</exception>
    public static implicit operator NodaTime.Period(JsonDuration value)
    {
        return (NodaTime.Period)value.GetPeriod();
    }

    /// <summary>
    /// Implicit conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonDuration(NodaTime.Period value)
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
    /// Try to get the value as a Period.
    /// </summary>
    /// <param name="result">The Period.</param>
    /// <returns><c>True</c> if it was possible to get a Period value from the instance.</returns>
    public bool TryGetPeriod([NotNullWhen(true)] out Period result)
    {
        if ((this.backing & Backing.String) != 0)
        {
            if (Period.PeriodParser(this.stringBacking, out PeriodBuilder builder))
            {
                result = builder.BuildPeriod();
                return true;
            }

            result = default;
            return false;
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            if (this.jsonElementBacking.TryGetValue(static (ReadOnlySpan<char> text, in object? _, out PeriodBuilder builder) => Period.PeriodParser(text, out builder), default, out PeriodBuilder builder))
            {
                result = builder.BuildPeriod();
                return true;
            }

            result = default;
            return false;
        }

        result = Period.Zero;
        return false;
    }

    private static string FormatPeriod(Period value)
    {
        return value.ToString();
    }

    private static string FormatPeriod(NodaTime.Period value)
    {
        return NodaTime.Text.PeriodPattern.NormalizingIso.Format(value);
    }
}