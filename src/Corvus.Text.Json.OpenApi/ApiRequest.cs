// <copyright file="ApiRequest.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;

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
/// Parameter values are stored as <see cref="JsonElement"/> instances with their
/// OpenAPI <see cref="ParameterStyle"/> and <c>explode</c> metadata. The transport
/// is responsible for serializing values according to these rules — no eager
/// <c>ToString()</c> conversion is performed by the generated code.
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
    /// The maximum number of query parameters, headers, or path parameters
    /// that can be stored inline without heap allocation.
    /// </summary>
    public const int MaxInlineParameters = 8;

    private ParameterBuffer pathParams;
    private ParameterBuffer queryParams;
    private ParameterBuffer headers;
    private int pathCount;
    private int queryCount;
    private int headerCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiRequest"/> struct.
    /// </summary>
    /// <param name="pathTemplate">The path template with <c>{name}</c> placeholders.</param>
    /// <param name="method">The operation method.</param>
    public ApiRequest(string pathTemplate, OperationMethod method)
    {
        this.PathTemplate = pathTemplate;
        this.Method = method;
    }

    /// <summary>
    /// Gets the path template with <c>{name}</c> placeholders for path parameters.
    /// The transport substitutes these using the <see cref="PathParameters"/> entries
    /// and their serialization style.
    /// </summary>
    public string PathTemplate { get; }

    /// <summary>
    /// Gets the operation method.
    /// </summary>
    public OperationMethod Method { get; }

    /// <summary>
    /// Gets the path parameters that substitute <c>{name}</c> placeholders
    /// in the <see cref="PathTemplate"/>.
    /// </summary>
    [UnscopedRef]
    public readonly ReadOnlySpan<ParameterEntry> PathParameters
    {
        get
        {
            ReadOnlySpan<ParameterEntry> span = this.pathParams;
            return span[..this.pathCount];
        }
    }

    /// <summary>
    /// Gets the request headers.
    /// </summary>
    [UnscopedRef]
    public readonly ReadOnlySpan<ParameterEntry> Headers
    {
        get
        {
            ReadOnlySpan<ParameterEntry> span = this.headers;
            return span[..this.headerCount];
        }
    }

    /// <summary>
    /// Gets the query parameters.
    /// </summary>
    [UnscopedRef]
    public readonly ReadOnlySpan<ParameterEntry> QueryParameters
    {
        get
        {
            ReadOnlySpan<ParameterEntry> span = this.queryParams;
            return span[..this.queryCount];
        }
    }

    /// <summary>
    /// Adds a path parameter to this request.
    /// </summary>
    /// <typeparam name="T">The type of the parameter value.</typeparam>
    /// <param name="name">The path parameter name (matching a <c>{name}</c> placeholder).</param>
    /// <param name="value">The parameter value.</param>
    /// <param name="style">The serialization style. Defaults to <see cref="ParameterStyle.Simple"/>.</param>
    /// <param name="explode">Whether to explode array/object values. Defaults to <see langword="false"/>.</param>
    /// <exception cref="InvalidOperationException">
    /// More than <see cref="MaxInlineParameters"/> path parameters have been added.
    /// </exception>
    public void AddPathParameter<T>(string name, in T value, ParameterStyle style = ParameterStyle.Simple, bool explode = false)
        where T : struct, IJsonElement<T>
    {
        if (this.pathCount >= MaxInlineParameters)
        {
            ThrowTooManyPathParameters();
        }

        this.pathParams[this.pathCount++] = new(name, JsonElement.From(in value), style, explode);
    }

    /// <summary>
    /// Adds a header to this request.
    /// </summary>
    /// <typeparam name="T">The type of the header value.</typeparam>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <param name="style">The serialization style. Defaults to <see cref="ParameterStyle.Simple"/>.</param>
    /// <param name="explode">Whether to explode array/object values. Defaults to <see langword="false"/>.</param>
    /// <exception cref="InvalidOperationException">
    /// More than <see cref="MaxInlineParameters"/> headers have been added.
    /// </exception>
    public void AddHeader<T>(string name, in T value, ParameterStyle style = ParameterStyle.Simple, bool explode = false)
        where T : struct, IJsonElement<T>
    {
        if (this.headerCount >= MaxInlineParameters)
        {
            ThrowTooManyHeaders();
        }

        this.headers[this.headerCount++] = new(name, JsonElement.From(in value), style, explode);
    }

    /// <summary>
    /// Adds a query parameter to this request.
    /// </summary>
    /// <typeparam name="T">The type of the parameter value.</typeparam>
    /// <param name="name">The query parameter name.</param>
    /// <param name="value">The query parameter value.</param>
    /// <param name="style">The serialization style. Defaults to <see cref="ParameterStyle.Form"/>.</param>
    /// <param name="explode">Whether to explode array/object values. Defaults to <see langword="true"/>.</param>
    /// <exception cref="InvalidOperationException">
    /// More than <see cref="MaxInlineParameters"/> query parameters have been added.
    /// </exception>
    public void AddQueryParameter<T>(string name, in T value, ParameterStyle style = ParameterStyle.Form, bool explode = true)
        where T : struct, IJsonElement<T>
    {
        if (this.queryCount >= MaxInlineParameters)
        {
            ThrowTooManyQueryParameters();
        }

        this.queryParams[this.queryCount++] = new(name, JsonElement.From(in value), style, explode);
    }

    [DoesNotReturn]
    private static void ThrowTooManyPathParameters() =>
        throw new InvalidOperationException(
            $"Cannot add more than {MaxInlineParameters} path parameters to an API request.");

    [DoesNotReturn]
    private static void ThrowTooManyHeaders() =>
        throw new InvalidOperationException(
            $"Cannot add more than {MaxInlineParameters} headers to an API request.");

    [DoesNotReturn]
    private static void ThrowTooManyQueryParameters() =>
        throw new InvalidOperationException(
            $"Cannot add more than {MaxInlineParameters} query parameters to an API request.");

    [InlineArray(MaxInlineParameters)]
    private struct ParameterBuffer
    {
        private ParameterEntry element0;
    }
}