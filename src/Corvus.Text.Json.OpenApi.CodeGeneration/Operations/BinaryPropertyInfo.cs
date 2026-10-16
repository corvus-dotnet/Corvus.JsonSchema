// <copyright file="BinaryPropertyInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// A multipart property, or a multipart response part, that carries binary content.
/// </summary>
/// <param name="PropertyName">The property name as it appears in the schema and on the wire.</param>
/// <param name="ContentType">The part's content type from its <c>encoding</c>, or <see langword="null"/> for the default.</param>
/// <param name="IsRequired">Whether the part is required.</param>
public readonly record struct BinaryPropertyInfo(
    string PropertyName,
    string? ContentType,
    bool IsRequired = false);