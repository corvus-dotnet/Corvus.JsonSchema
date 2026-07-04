// <copyright file="ResponseInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of an OpenAPI operation response.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Summary"/> and <see cref="Description"/> are populated only by the OpenAPI 3.2
/// walker; they are appended after the common prefix so that the 3.0 and 3.1 walkers can
/// continue to construct the common fields positionally.
/// </para>
/// </remarks>
/// <param name="StatusCode">The HTTP status code (or <c>default</c>).</param>
/// <param name="Content">The response content entries.</param>
/// <param name="Headers">The response headers.</param>
/// <param name="Links">The response links.</param>
/// <param name="Summary">The optional summary (OpenAPI 3.2). Only populated by the 3.2 walker.</param>
/// <param name="Description">
/// The optional description (OpenAPI 3.2). Only populated by the 3.2 walker.
/// </param>
public readonly record struct ResponseInfo(
    string StatusCode,
    ContentInfo[] Content,
    HeaderInfo[] Headers,
    LinkInfo[] Links,
    string? Summary = null,
    string? Description = null);