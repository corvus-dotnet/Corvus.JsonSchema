// <copyright file="SchemaRole.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Identifies the role a schema plays within an API specification.
/// </summary>
public enum SchemaRole
{
    /// <summary>
    /// Schema for an HTTP request body (OpenAPI).
    /// </summary>
    RequestBody,

    /// <summary>
    /// Schema for an HTTP response body (OpenAPI).
    /// </summary>
    ResponseBody,

    /// <summary>
    /// Schema for a path, query, cookie, or header parameter (OpenAPI).
    /// </summary>
    Parameter,

    /// <summary>
    /// Schema for an HTTP header (OpenAPI and AsyncAPI).
    /// </summary>
    Header,

    /// <summary>
    /// Schema for a message payload (AsyncAPI).
    /// </summary>
    MessagePayload,

    /// <summary>
    /// Schema for a message header (AsyncAPI).
    /// </summary>
    MessageHeader,

    /// <summary>
    /// Schema defined in the components/schemas section, not directly tied to an operation.
    /// </summary>
    ComponentSchema,
}