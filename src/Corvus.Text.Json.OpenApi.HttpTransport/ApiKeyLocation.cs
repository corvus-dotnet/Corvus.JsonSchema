// <copyright file="ApiKeyLocation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.HttpTransport;

/// <summary>
/// Specifies where an API key is placed in an HTTP request,
/// corresponding to the OpenAPI <c>in</c> field of an <c>apiKey</c>
/// security scheme.
/// </summary>
public enum ApiKeyLocation
{
    /// <summary>
    /// The API key is sent as a request header.
    /// </summary>
    Header,

    /// <summary>
    /// The API key is appended as a query string parameter.
    /// </summary>
    Query,

    /// <summary>
    /// The API key is sent as a cookie.
    /// </summary>
    Cookie,
}