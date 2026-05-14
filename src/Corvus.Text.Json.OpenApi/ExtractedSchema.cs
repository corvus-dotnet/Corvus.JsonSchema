// <copyright file="ExtractedSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Represents a schema extracted from an API specification.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Schema"/> element is a reference into the parsed document
/// and is only valid while the document is alive.
/// </para>
/// </remarks>
/// <param name="Schema">
/// The schema element. This may be an inline schema object or a <c>$ref</c> object
/// pointing to a component schema.
/// </param>
/// <param name="Role">
/// The role this schema plays (request body, response body, parameter, etc.).
/// </param>
public readonly record struct ExtractedSchema(
    JsonElement Schema,
    SchemaRole Role);