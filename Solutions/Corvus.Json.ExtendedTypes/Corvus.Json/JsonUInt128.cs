// <copyright file="JsonUInt128.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON uint128.
/// </summary>
public readonly partial struct JsonUInt128
{
    /// <summary>
    /// Gets the value as a UInt128.
    /// </summary>
    /// <returns>The UInt128 value.</returns>
    public UInt128 AsUInt128() => (UInt128)this;
}