// <copyright file="JsonInt128.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON int128.
/// </summary>
public readonly partial struct JsonInt128
{
    /// <summary>
    /// Gets the value as in Int128.
    /// </summary>
    /// <returns>The Int128 value.</returns>
    public Int128 AsInt128() => (Int128)this;
}