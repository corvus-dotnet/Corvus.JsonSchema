// <copyright file="ParameterInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of an OpenAPI operation parameter.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AllowReserved"/> is populated only by the OpenAPI 3.2 walker; it is appended
/// after the common prefix so that the 3.0 and 3.1 walkers can continue to construct the
/// common fields positionally.
/// </para>
/// </remarks>
/// <param name="Name">The parameter name.</param>
/// <param name="Location">The parameter location.</param>
/// <param name="IsRequired">Whether the parameter is required.</param>
/// <param name="Style">The serialization style.</param>
/// <param name="Explode">Whether the parameter is exploded.</param>
/// <param name="SerializationKind">The serialization kind of the value.</param>
/// <param name="ElementSerializationKind">The serialization kind of array/object elements.</param>
/// <param name="SchemaPointer">The optional schema pointer.</param>
/// <param name="HasDeepNesting">Whether the parameter schema has deep nesting.</param>
/// <param name="DefaultValueJson">The optional default value as JSON.</param>
/// <param name="DefaultValueKind">The JSON value kind of the default value.</param>
/// <param name="AllowReserved">
/// Whether reserved characters are permitted unescaped (OpenAPI 3.2). Only populated by the
/// 3.2 walker.
/// </param>
public readonly record struct ParameterInfo(
    string Name,
    ParameterLocation Location,
    bool IsRequired,
    ParameterStyle Style,
    bool Explode,
    ParameterSerializationKind SerializationKind,
    ParameterSerializationKind ElementSerializationKind,
    string? SchemaPointer,
    bool HasDeepNesting,
    string? DefaultValueJson,
    JsonValueKind DefaultValueKind,
    bool AllowReserved = false);