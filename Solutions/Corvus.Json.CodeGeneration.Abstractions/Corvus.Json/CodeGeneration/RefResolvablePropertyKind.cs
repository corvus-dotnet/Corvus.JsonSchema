// <copyright file="RefResolvablePropertyKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Determines the strategy for idenfying the schema in reference resolvables.
/// </summary>
public enum RefResolvablePropertyKind
{
    /// <summary>
    /// The property value is not ref resolvable.
    /// </summary>
    None,

    /// <summary>
    /// The property value is a schema.
    /// </summary>
    Schema,

    /// <summary>
    /// The property value is an array of schema.
    /// </summary>
    ArrayOfSchema,

    /// <summary>
    /// The property value is either an array of schema or a schema.
    /// </summary>
    SchemaOrArrayOfSchema,

    /// <summary>
    /// The property value is a map of schema.
    /// </summary>
    MapOfSchema,

    /// <summary>
    /// The property value is either a schema if it is an object, or not a schema if it is not an object.
    /// </summary>
    /// <remarks>
    /// This is a legacy construct from draft6 and draft7 and has been eliminated from 2019-09 onwards.
    /// </remarks>
    SchemaIfValueIsSchemaLike,

    /// <summary>
    /// The property value is a map of schema if the values are objects.
    /// </summary>
    /// <remarks>
    /// This is a legacy construct from draft6 and draft7 and has been eliminated from 2019-09 onwards.
    /// </remarks>
    MapOfSchemaIfValueIsSchemaLike,
}