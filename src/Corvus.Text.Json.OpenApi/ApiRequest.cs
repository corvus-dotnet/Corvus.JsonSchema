// <copyright file="ApiRequest.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Represents an outgoing API request built by generated client code
/// and dispatched by an <see cref="IApiTransport"/>.
/// </summary>
public sealed class ApiRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiRequest"/> class.
    /// </summary>
    /// <param name="path">The resolved path (template parameters already substituted).</param>
    /// <param name="method">The HTTP method.</param>
    public ApiRequest(string path, string method)
    {
        this.Path = path;
        this.Method = method;
        this.Headers = ImmutableDictionary<string, string>.Empty;
        this.QueryParameters = ImmutableDictionary<string, string>.Empty;
    }

    private ApiRequest(
        string path,
        string method,
        ImmutableDictionary<string, string> headers,
        ImmutableDictionary<string, string> queryParameters,
        ReadOnlyMemory<byte>? body,
        string? contentType)
    {
        this.Path = path;
        this.Method = method;
        this.Headers = headers;
        this.QueryParameters = queryParameters;
        this.Body = body;
        this.ContentType = contentType;
    }

    /// <summary>
    /// Gets the resolved request path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the HTTP method (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// Gets the request headers.
    /// </summary>
    public ImmutableDictionary<string, string> Headers { get; }

    /// <summary>
    /// Gets the query parameters.
    /// </summary>
    public ImmutableDictionary<string, string> QueryParameters { get; }

    /// <summary>
    /// Gets the request body, if any.
    /// </summary>
    public ReadOnlyMemory<byte>? Body { get; }

    /// <summary>
    /// Gets the content type of the request body, if any.
    /// </summary>
    public string? ContentType { get; }

    /// <summary>
    /// Returns a new request with the specified header added.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>A new <see cref="ApiRequest"/> with the header.</returns>
    public ApiRequest WithHeader(string name, string value) =>
        new(this.Path, this.Method, this.Headers.SetItem(name, value), this.QueryParameters, this.Body, this.ContentType);

    /// <summary>
    /// Returns a new request with the specified query parameter added.
    /// </summary>
    /// <param name="name">The query parameter name.</param>
    /// <param name="value">The query parameter value.</param>
    /// <returns>A new <see cref="ApiRequest"/> with the query parameter.</returns>
    public ApiRequest WithQueryParameter(string name, string value) =>
        new(this.Path, this.Method, this.Headers, this.QueryParameters.SetItem(name, value), this.Body, this.ContentType);

    /// <summary>
    /// Returns a new request with the specified body.
    /// </summary>
    /// <param name="body">The body bytes.</param>
    /// <param name="contentType">The content type.</param>
    /// <returns>A new <see cref="ApiRequest"/> with the body.</returns>
    public ApiRequest WithBody(ReadOnlyMemory<byte> body, string contentType = "application/json") =>
        new(this.Path, this.Method, this.Headers, this.QueryParameters, body, contentType);
}