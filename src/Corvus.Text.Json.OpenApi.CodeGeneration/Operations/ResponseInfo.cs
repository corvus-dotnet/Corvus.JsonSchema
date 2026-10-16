// <copyright file="ResponseInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// A response prepared for emission.
/// </summary>
/// <param name="StatusCode">The status code (<c>200</c>), range (<c>2XX</c>) or <c>default</c> the response is keyed by.</param>
/// <param name="Summary">The response's <c>summary</c> (OpenAPI 3.2), or <see langword="null"/>.</param>
/// <param name="Description">The response's <c>description</c>, or <see langword="null"/>.</param>
/// <param name="Content">The response's media types, in document order; empty for a response without a body.</param>
/// <param name="Headers">The response headers, in document order.</param>
/// <param name="Links">The response's links, in document order.</param>
/// <param name="BinaryParts">For a multipart response, the parts that carry binary content; otherwise empty.</param>
public readonly record struct ResponseInfo(
    string StatusCode,
    string? Summary,
    string? Description,
    ContentInfo[] Content,
    HeaderInfo[] Headers,
    LinkInfo[] Links,
    BinaryPropertyInfo[] BinaryParts);