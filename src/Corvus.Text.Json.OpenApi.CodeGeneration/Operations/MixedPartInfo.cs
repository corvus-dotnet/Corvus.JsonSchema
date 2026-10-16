// <copyright file="MixedPartInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// A positional part of an OpenAPI 3.2 <c>multipart/mixed</c> body.
/// </summary>
/// <param name="SchemaPointer">The JSON Pointer of the part's schema within the document.</param>
/// <param name="ContentType">The part's content type.</param>
/// <param name="IsBinary">Whether the part carries binary content rather than JSON.</param>
public readonly record struct MixedPartInfo(
    string SchemaPointer,
    string ContentType,
    bool IsBinary);