// <copyright file="ContentInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// One media type of a request or response body.
/// </summary>
/// <param name="MediaType">The media type as written in the document.</param>
/// <param name="SchemaPointer">The JSON Pointer of the media type's schema within the document, or <see langword="null"/> when it has none.</param>
/// <param name="Encodings">The per-property <c>encoding</c> entries, keyed by property name, or <see langword="null"/> when there are none.</param>
/// <param name="ItemSchemaPointer">For an OpenAPI 3.2 streaming media type, the JSON Pointer of its <c>itemSchema</c>; otherwise <see langword="null"/>.</param>
public readonly record struct ContentInfo(
    string MediaType,
    string? SchemaPointer,
    IReadOnlyDictionary<string, ContentEncodingInfo>? Encodings,
    string? ItemSchemaPointer = null);