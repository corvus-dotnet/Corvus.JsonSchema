// <copyright file="RuntimeExpressionKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Identifies the kind of an OpenAPI runtime expression.
/// </summary>
public enum RuntimeExpressionKind
{
    /// <summary>
    /// A literal value (not a runtime expression). The value is passed as-is.
    /// </summary>
    Literal,

    /// <summary>
    /// <c>$url</c> — the full request URL.
    /// </summary>
    Url,

    /// <summary>
    /// <c>$method</c> — the HTTP method used for the request.
    /// </summary>
    Method,

    /// <summary>
    /// <c>$response.body#/pointer</c> — a JSON Pointer into the response body.
    /// </summary>
    ResponseBody,

    /// <summary>
    /// <c>$response.header.name</c> — a named response header value.
    /// </summary>
    ResponseHeader,

    /// <summary>
    /// <c>$request.body#/pointer</c> — a JSON Pointer into the request body.
    /// </summary>
    RequestBody,

    /// <summary>
    /// <c>$request.path.name</c> — a named path parameter from the request.
    /// </summary>
    RequestPath,

    /// <summary>
    /// <c>$request.query.name</c> — a named query parameter from the request.
    /// </summary>
    RequestQuery,

    /// <summary>
    /// <c>$request.header.name</c> — a named request header value.
    /// </summary>
    RequestHeader,
}