// <copyright file="HeaderInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of an OpenAPI response header.
/// </summary>
/// <param name="HeaderName">The header name.</param>
/// <param name="SchemaPointer">The optional schema pointer.</param>
/// <param name="Explode">Whether the header value is exploded.</param>
/// <param name="SerializationKind">The serialization kind of the value.</param>
/// <param name="ElementSerializationKind">The serialization kind of array/object elements.</param>
/// <param name="HasDeepNesting">Whether the header schema has deep nesting.</param>
public readonly record struct HeaderInfo(
    string HeaderName,
    string? SchemaPointer,
    bool Explode,
    ParameterSerializationKind SerializationKind,
    ParameterSerializationKind ElementSerializationKind,
    bool HasDeepNesting);