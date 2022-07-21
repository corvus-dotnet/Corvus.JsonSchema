// <copyright file="JsonInteger.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON date.
/// </summary>
public readonly partial struct JsonInteger
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonInteger"/> struct.
    /// </summary>
    /// <param name="value">The integer value.</param>
    public JsonInteger(long value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = value;
    }
}