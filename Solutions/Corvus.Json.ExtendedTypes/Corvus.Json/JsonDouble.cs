// <copyright file="JsonDouble.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON double.
/// </summary>
public readonly partial struct JsonDouble
{
    /// <summary>
    /// Gets the value as a double.
    /// </summary>
    /// <returns>The double value.</returns>
    public double AsDouble() => (double)this;
}