// <copyright file="OperationMethod.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// The HTTP method of an OpenAPI operation, or the messaging action of an AsyncAPI operation.
/// </summary>
public enum OperationMethod
{
    /// <summary>HTTP GET.</summary>
    Get,

    /// <summary>HTTP PUT.</summary>
    Put,

    /// <summary>HTTP POST.</summary>
    Post,

    /// <summary>HTTP DELETE.</summary>
    Delete,

    /// <summary>HTTP OPTIONS.</summary>
    Options,

    /// <summary>HTTP HEAD.</summary>
    Head,

    /// <summary>HTTP PATCH.</summary>
    Patch,

    /// <summary>HTTP TRACE.</summary>
    Trace,

    /// <summary>HTTP QUERY (RFC 9110).</summary>
    Query,

    /// <summary>AsyncAPI publish action.</summary>
    Publish,

    /// <summary>AsyncAPI subscribe action.</summary>
    Subscribe,

    /// <summary>A custom HTTP method (from OpenAPI 3.2 additionalOperations).</summary>
    Custom,
}