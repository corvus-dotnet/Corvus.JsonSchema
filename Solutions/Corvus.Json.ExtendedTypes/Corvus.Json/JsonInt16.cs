// <copyright file="JsonInt16.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON int16.
/// </summary>
public readonly partial struct JsonInt16
{
    /// <summary>
    /// Gets the value as a short.
    /// </summary>
    /// <returns>The short value.</returns>
    public short AsShort() => (short)this;
}