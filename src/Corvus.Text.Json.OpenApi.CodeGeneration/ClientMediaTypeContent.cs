// <copyright file="ClientMediaTypeContent.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a content entry (media type → schema) in a request body or response.
/// </summary>
/// <param name="MediaType">The media type (e.g. <c>application/json</c>).</param>
/// <param name="SchemaPointer">
/// The JSON pointer to the schema within the spec document,
/// or <see langword="null"/> if no schema is defined.
/// </param>
public sealed record ClientMediaTypeContent(
    string MediaType,
    string? SchemaPointer);