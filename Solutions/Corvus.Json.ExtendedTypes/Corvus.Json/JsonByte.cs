// <copyright file="JsonByte.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON byte.
/// </summary>
public readonly partial struct JsonByte
{
    /// <summary>
    /// Gets the value as a byte.
    /// </summary>
    /// <returns>The byte value.</returns>
    public byte AsByte() => (byte)this;
}