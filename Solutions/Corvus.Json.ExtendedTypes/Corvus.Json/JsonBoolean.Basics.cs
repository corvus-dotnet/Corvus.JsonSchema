// <copyright file="JsonBoolean.Basics.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
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
    /// Conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonBoolean(JsonAny value)
    {
        return value.As<JsonBoolean>();
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
    public static explicit operator bool(JsonBoolean value)
    {
        return value.GetBoolean() ?? throw new InvalidOperationException();
    }

    /// <summary>
    /// Try to retrieve the value as a boolean.
    /// </summary>
    /// <param name="result"><see langword="true"/> if the value was true, otherwise <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the value was representable as a boolean, otherwise <see langword="false"/>.</returns>
    public bool TryGetBoolean([NotNullWhen(true)] out bool result)
    {
        switch (this.ValueKind)
        {
            case JsonValueKind.True:
                result = true;
                return true;
            case JsonValueKind.False:
                result = false;
                return true;
            default:
                result = default;
                return false;
        }
    }

    /// <summary>
    /// Get the value as a boolean.
    /// </summary>
    /// <returns>The value of the boolean, or <see langword="null"/> if the value was not representable as a boolean.</returns>
    public bool? GetBoolean()
    {
        if (this.TryGetBoolean(out bool result))
        {
            return result;
        }

        return null;
    }
}