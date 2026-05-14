// <copyright file="ClientParameter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a parameter in an API operation.
/// </summary>
/// <param name="Name">The parameter name as declared in the spec.</param>
/// <param name="Location">Where the parameter appears (path, query, header, cookie).</param>
/// <param name="IsRequired">Whether the parameter is required.</param>
/// <param name="SchemaPointer">
/// The JSON pointer to the parameter's schema within the spec document
/// (e.g. <c>#/paths/~1pets/get/parameters/0/schema</c>), or <see langword="null"/>
/// if the parameter has no schema.
/// </param>
/// <param name="Description">Optional description from the spec.</param>
public sealed record ClientParameter(
    string Name,
    ParameterLocation Location,
    bool IsRequired,
    string? SchemaPointer,
    string? Description);