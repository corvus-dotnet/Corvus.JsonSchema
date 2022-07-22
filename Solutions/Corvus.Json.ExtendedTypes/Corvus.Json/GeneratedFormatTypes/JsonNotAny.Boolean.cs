// <copyright file="JsonNotAny.Boolean.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents any JSON value, validating false.
/// </summary>
public readonly partial struct JsonNotAny : IJsonBoolean<JsonNotAny>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonNotAny(bool value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Bool;
        this.stringBacking = string.Empty;
        this.boolBacking = value;
        this.numberBacking = default;
        this.arrayBacking = ImmutableList<JsonAny>.Empty;
        this.objectBacking = ImmutableDictionary<JsonPropertyName, JsonAny>.Empty;
    }

    /// <summary>
    /// Conversion from bool.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNotAny(bool value)
    {
        return new JsonNotAny(value);
    }

    /// <summary>
    /// Conversion to bool.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    public static implicit operator bool(JsonNotAny value)
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