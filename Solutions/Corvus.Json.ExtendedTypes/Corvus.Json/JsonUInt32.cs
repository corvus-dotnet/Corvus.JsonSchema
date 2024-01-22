// <copyright file="JsonUInt32.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON uint32.
/// </summary>
public readonly partial struct JsonUInt32
{
    /// <summary>
    /// Gets the value as a uint.
    /// </summary>
    /// <returns>The uint value.</returns>
    public uint AsUInt32() => (uint)this;
}