// <copyright file="HeaderInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// A response header prepared for emission.
/// </summary>
/// <param name="HeaderName">The header name as it appears on the wire.</param>
/// <param name="SchemaPointer">The JSON Pointer of the header's schema within the document, or <see langword="null"/> when it has none.</param>
/// <param name="Explode">The <c>explode</c> setting, after defaults have been applied.</param>
/// <param name="SerializationKind">How the header's value is written, classified from its schema.</param>
/// <param name="ElementSerializationKind">For an array header, how each element is written; otherwise <see cref="ParameterSerializationKind.None"/>.</param>
/// <param name="HasDeepNesting">Whether the schema nests objects or arrays beyond what the header style can represent.</param>
public readonly record struct HeaderInfo(
    string HeaderName,
    string? SchemaPointer,
    bool Explode,
    ParameterSerializationKind SerializationKind,
    ParameterSerializationKind ElementSerializationKind,
    bool HasDeepNesting);