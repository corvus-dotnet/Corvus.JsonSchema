// <copyright file="JsonInteger.Basics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON integer.
/// </summary>
public readonly partial struct JsonInteger
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    private JsonInteger(double value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = value;
    }

    /// <summary>
    /// Conversion to JsonNumber.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNumber(JsonInteger value)
    {
        return value.AsNumber;
    }

    /// <summary>
    /// Conversion to JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(JsonInteger value)
    {
        return value.AsAny;
    }

    /// <summary>
    /// Conversion to double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    /// <exception cref="FormatException">The value was not formatted as a double.</exception>
    public static explicit operator double(JsonInteger value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.GetDouble();
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return value.numberBacking;
        }

        throw new InvalidOperationException();
    }
}