// <copyright file="JsonUInt16.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON uint16.
/// </summary>
public readonly partial struct JsonUInt16
{
    /// <summary>
    /// Gets the value as a ushort.
    /// </summary>
    /// <returns>The ushort value.</returns>
    public ushort AsUInt16() => (ushort)this;
}