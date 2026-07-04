// <copyright file="BinaryPropertyInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of a binary property within a request body.
/// </summary>
/// <param name="PropertyName">The property name.</param>
/// <param name="ParameterName">The generated parameter name.</param>
/// <param name="ContentType">The optional content type.</param>
public readonly record struct BinaryPropertyInfo(
    string PropertyName,
    string ParameterName,
    string? ContentType);