// <copyright file="JsonBoolean.Basics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a Json boolean.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Corvus.Json.Internal.JsonValueConverter<JsonBoolean>))]
public readonly partial struct JsonBoolean
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonBoolean(bool value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Bool;
        this.boolBacking = value;
    }

    /// <summary>
    /// Conversion to JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(JsonBoolean value)
    {
        return value.AsAny;
    }

    /// <summary>
    /// Conversion from bool.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonBoolean(bool value)
    {
        return new JsonBoolean(value);
    }

    /// <summary>
    /// Conversion to bool.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    public static implicit operator bool(JsonBoolean value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return value.jsonElementBacking.GetBoolean();
        }

        if ((value.backing & Backing.Bool) != 0)
        {
            return value.boolBacking;
        }

        throw new InvalidOperationException();
    }
}