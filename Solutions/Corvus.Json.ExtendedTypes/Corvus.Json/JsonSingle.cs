// <copyright file="JsonSingle.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON single.
/// </summary>
public readonly partial struct JsonSingle
{
    /// <summary>
    /// Gets the value as a float.
    /// </summary>
    /// <returns>The float value.</returns>
    public float AsSingle() => (float)this;
}