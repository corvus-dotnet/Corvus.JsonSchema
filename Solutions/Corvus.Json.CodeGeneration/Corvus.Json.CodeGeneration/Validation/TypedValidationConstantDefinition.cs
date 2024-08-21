// <copyright file="TypedValidationConstantDefinition.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// The definition of a strongly-typed validation constant based on a well-known format.
/// </summary>
/// <param name="format">The format of the constant value.</param>
/// <param name="value">The JSON value of the constant.</param>
public readonly struct TypedValidationConstantDefinition(string format, JsonElement value)
{
    /// <summary>
    /// Gets the format for the constant.
    /// </summary>
    public string Format { get; } = format;

    /// <summary>
    /// Gets the <see cref="JsonElement"/> representing the constant value.
    /// </summary>
    public JsonElement Value { get; } = value;
}