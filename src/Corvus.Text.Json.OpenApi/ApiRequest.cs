// <copyright file="ApiRequest.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Represents the metadata for an outgoing API request built by generated client code
/// and dispatched by an <see cref="IApiTransport"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a stack-allocated value type with inline storage for up to
/// <see cref="MaxInlineParameters"/> query parameters and headers each.
/// No heap allocation occurs on the request hot path for typical APIs.
/// </para>
/// <para>
/// The request carries only metadata — path, method, query parameters, and headers.
/// Request bodies are not stored in this struct. Instead, the transport's generic
/// <see cref="IApiTransport.SendAsync{TBody}"/> overload accepts the typed body
/// separately and writes it directly into the transport's output stream.
/// </para>
/// </remarks>
public struct ApiRequest
{
    /// <summary>
    /// The maximum number of query parameters or headers that can be stored
    /// inline without heap allocation.
    /// </summary>
    public const int MaxInlineParameters = 8;

    private KvpBuffer queryParams;
    private KvpBuffer headers;
    private int queryCount;
    private int headerCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiRequest"/> struct.
    /// </summary>
    /// <param name="path">The resolved path (template parameters already substituted).</param>
    /// <param name="method">The operation method.</param>
    public ApiRequest(string path, OperationMethod method)
    {
        this.Path = path;
        this.Method = method;
    }

    /// <summary>
    /// Gets the resolved request path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the operation method.
    /// </summary>
    public OperationMethod Method { get; }

    /// <summary>
    /// Gets the request headers.
    /// </summary>
    [UnscopedRef]
    public readonly ReadOnlySpan<KeyValuePair<string, string>> Headers
    {
        get
        {
            ReadOnlySpan<KeyValuePair<string, string>> span = this.headers;
            return span[..this.headerCount];
        }
    }

    /// <summary>
    /// Gets the query parameters.
    /// </summary>
    [UnscopedRef]
    public readonly ReadOnlySpan<KeyValuePair<string, string>> QueryParameters
    {
        get
        {
            ReadOnlySpan<KeyValuePair<string, string>> span = this.queryParams;
            return span[..this.queryCount];
        }
    }

    /// <summary>
    /// Adds a header to this request.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <exception cref="InvalidOperationException">
    /// More than <see cref="MaxInlineParameters"/> headers have been added.
    /// </exception>
    public void AddHeader(string name, string value)
    {
        if (this.headerCount >= MaxInlineParameters)
        {
            ThrowTooManyHeaders();
        }

        this.headers[this.headerCount++] = new(name, value);
    }

    /// <summary>
    /// Adds a query parameter to this request.
    /// </summary>
    /// <param name="name">The query parameter name.</param>
    /// <param name="value">The query parameter value.</param>
    /// <exception cref="InvalidOperationException">
    /// More than <see cref="MaxInlineParameters"/> query parameters have been added.
    /// </exception>
    public void AddQueryParameter(string name, string value)
    {
        if (this.queryCount >= MaxInlineParameters)
        {
            ThrowTooManyQueryParameters();
        }

        this.queryParams[this.queryCount++] = new(name, value);
    }

    [DoesNotReturn]
    private static void ThrowTooManyHeaders() =>
        throw new InvalidOperationException(
            $"Cannot add more than {MaxInlineParameters} headers to an API request.");

    [DoesNotReturn]
    private static void ThrowTooManyQueryParameters() =>
        throw new InvalidOperationException(
            $"Cannot add more than {MaxInlineParameters} query parameters to an API request.");

    [InlineArray(MaxInlineParameters)]
    private struct KvpBuffer
    {
        private KeyValuePair<string, string> element0;
    }
}