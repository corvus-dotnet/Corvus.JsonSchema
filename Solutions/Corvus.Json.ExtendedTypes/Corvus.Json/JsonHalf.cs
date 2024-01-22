// <copyright file="JsonHalf.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON half.
/// </summary>
public readonly partial struct JsonHalf
{
    /// <summary>
    /// Gets the value as a half.
    /// </summary>
    /// <returns>The half value.</returns>
    public Half AsHalf() => (Half)this;
}