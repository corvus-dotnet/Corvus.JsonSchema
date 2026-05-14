// <copyright file="ClientRequestBody.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents the request body of an API operation.
/// </summary>
/// <param name="IsRequired">Whether the request body is required.</param>
/// <param name="Content">
/// The content entries, keyed by media type. Each entry maps a media type
/// (e.g. <c>application/json</c>) to its schema pointer.
/// </param>
/// <param name="Description">Optional description from the spec.</param>
public sealed record ClientRequestBody(
    bool IsRequired,
    IReadOnlyList<ClientMediaTypeContent> Content,
    string? Description);