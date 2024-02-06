// <copyright file="JsonInt64.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON int64.
/// </summary>
public readonly partial struct JsonInt64
{
    /// <summary>
    /// Gets the value as a long.
    /// </summary>
    /// <returns>The long value.</returns>
    public long AsInt64() => (long)this;
}