// <copyright file="MixedPartInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of a single part in an OpenAPI 3.2 sequential
/// multipart request body.
/// </summary>
/// <param name="SchemaPointer">The schema pointer for the part.</param>
/// <param name="ContentType">The content type of the part.</param>
/// <param name="IsBinary">Whether the part carries binary content.</param>
public readonly record struct MixedPartInfo(
    string SchemaPointer,
    string ContentType,
    bool IsBinary);