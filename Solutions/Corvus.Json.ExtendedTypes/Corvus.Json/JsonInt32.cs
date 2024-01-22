// <copyright file="JsonInt32.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON int32.
/// </summary>
public readonly partial struct JsonInt32
{
    /// <summary>
    /// Gets the value as an int.
    /// </summary>
    /// <returns>The int value.</returns>
    public int AsInt32() => (int)this;
}