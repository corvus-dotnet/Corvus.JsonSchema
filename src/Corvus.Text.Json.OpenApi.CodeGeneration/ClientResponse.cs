// <copyright file="ClientResponse.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents one response entry in an API operation.
/// </summary>
/// <param name="StatusCodePattern">
/// The HTTP status code or pattern (e.g. <c>200</c>, <c>default</c>, <c>2XX</c>).
/// </param>
/// <param name="Content">
/// The content entries for this response, keyed by media type.
/// Empty if the response has no body.
/// </param>
/// <param name="Description">Optional description from the spec.</param>
public sealed record ClientResponse(
    string StatusCodePattern,
    IReadOnlyList<ClientMediaTypeContent> Content,
    string? Description);