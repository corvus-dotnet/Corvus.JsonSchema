// <copyright file="JsonUInt64.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON uint64.
/// </summary>
public readonly partial struct JsonUInt64
{
    /// <summary>
    /// Gets the value as a ulong.
    /// </summary>
    /// <returns>The ulong value.</returns>
    public ulong AsUInt64() => (ulong)this;
}