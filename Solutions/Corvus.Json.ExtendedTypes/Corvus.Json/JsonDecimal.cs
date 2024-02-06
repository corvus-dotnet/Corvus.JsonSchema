// <copyright file="JsonDecimal.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents a JSON double.
/// </summary>
public readonly partial struct JsonDecimal
{
    /// <summary>
    /// Gets the value as a decimal.
    /// </summary>
    /// <returns>The decimal value.</returns>
    public decimal AsDecimal() => (decimal)this;
}