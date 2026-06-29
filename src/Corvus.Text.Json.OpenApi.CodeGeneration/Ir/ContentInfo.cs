// <copyright file="ContentInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of a single OpenAPI content (media type) entry.
/// </summary>
/// <param name="MediaType">The media type.</param>
/// <param name="SchemaPointer">The optional schema pointer.</param>
/// <param name="Encodings">The optional per-property encodings.</param>
/// <param name="ItemSchemaPointer">
/// The optional item schema pointer for sequential media types (OpenAPI 3.2).
/// </param>
public readonly record struct ContentInfo(
    string MediaType,
    string? SchemaPointer,
    IReadOnlyDictionary<string, EncodingInfo>? Encodings,
    string? ItemSchemaPointer = null);