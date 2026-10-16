// <copyright file="ParameterInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// A request parameter prepared for emission, with its wire serialisation resolved.
/// </summary>
/// <param name="Name">The parameter name as it appears on the wire.</param>
/// <param name="Location">Where the parameter is sent.</param>
/// <param name="IsRequired">Whether the parameter is required.</param>
/// <param name="Style">The serialisation style, after defaults have been applied for the location.</param>
/// <param name="Explode">The <c>explode</c> setting, after defaults have been applied for the style.</param>
/// <param name="AllowReserved">Whether reserved characters may appear unescaped in a query value (<c>allowReserved</c>).</param>
/// <param name="SerializationKind">How the parameter's value is written, classified from its schema.</param>
/// <param name="ElementSerializationKind">For an array parameter, how each element is written; otherwise <see cref="ParameterSerializationKind.None"/>.</param>
/// <param name="SchemaPointer">The JSON Pointer of the parameter's schema within the document, or <see langword="null"/> when it has none.</param>
/// <param name="HasDeepNesting">Whether the schema nests objects or arrays beyond what the wire styles can represent.</param>
/// <param name="DefaultValueJson">The schema's <c>default</c> as JSON text, or <see langword="null"/>.</param>
/// <param name="DefaultValueKind">The JSON kind of <paramref name="DefaultValueJson"/>, or <see cref="JsonValueKind.Undefined"/> when there is no default.</param>
/// <param name="DegradedCollectionFormat">
/// OpenAPI 2.0 only: whether the parameter's <c>collectionFormat</c> has no wire mapping at its location and
/// comma-separated semantics were substituted.
/// </param>
public readonly record struct ParameterInfo(
    string Name,
    ParameterLocation Location,
    bool IsRequired,
    ParameterStyle Style,
    bool Explode,
    bool AllowReserved,
    ParameterSerializationKind SerializationKind,
    ParameterSerializationKind ElementSerializationKind,
    string? SchemaPointer,
    bool HasDeepNesting,
    string? DefaultValueJson,
    JsonValueKind DefaultValueKind,
    bool DegradedCollectionFormat = false);