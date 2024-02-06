// <copyright file="JsonSByte.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON sbyte.
/// </summary>
public readonly partial struct JsonSByte
{
    /// <summary>
    /// Gets the value as an sbyte.
    /// </summary>
    /// <returns>The sbyte value.</returns>
    public sbyte AsSByte() => (sbyte)this;
}