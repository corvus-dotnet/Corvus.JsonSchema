// <copyright file="IApiRequest.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Represents a strongly-typed API request generated from an OpenAPI specification.
/// </summary>
/// <remarks>
/// <para>
/// Generated code produces a per-operation struct implementing this interface.
/// The struct carries typed parameter fields and bakes in the serialization rules
/// (style, explode, encoding) at compile time — no runtime metadata interpretation.
/// </para>
/// <para>
/// The transport calls the <c>Write*</c> methods to construct the request URI and headers
/// in UTF-8. Each method writes directly to an <see cref="IBufferWriter{T}"/> without
/// allocating intermediate strings.
/// </para>
/// <para>
/// The <see cref="PathTemplateUtf8"/> and <see cref="Method"/> properties are compile-time
/// constants exposed as static abstract members, enabling the transport to branch on them
/// without virtual dispatch.
/// </para>
/// </remarks>
public interface IApiRequest<TSelf>
    where TSelf : struct, IApiRequest<TSelf>
{
    /// <summary>
    /// Gets the raw path template in UTF-8 (e.g. <c>/pets/{petId}</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the unresolved template. For operations with no path parameters,
    /// this is the final path. For operations with path parameters, the transport
    /// should call <see cref="WriteResolvedPath"/> instead.
    /// </para>
    /// </remarks>
    static abstract ReadOnlySpan<byte> PathTemplateUtf8 { get; }

    /// <summary>
    /// Gets the HTTP method for this operation.
    /// </summary>
    static abstract OperationMethod Method { get; }

    /// <summary>
    /// Gets a value indicating whether this operation has any path parameters
    /// that require template substitution.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/>, the transport can use <see cref="PathTemplateUtf8"/>
    /// directly without calling <see cref="WriteResolvedPath"/>.
    /// </remarks>
    static abstract bool HasPathParameters { get; }

    /// <summary>
    /// Gets a value indicating whether this operation has any query parameters
    /// that will produce output.
    /// </summary>
    /// <remarks>
    /// This may depend on which optional parameters are set. When <see langword="false"/>
    /// at the static level, the transport can skip query string construction entirely.
    /// The instance-level <see cref="WriteQueryString"/> handles optional parameter presence.
    /// </remarks>
    static abstract bool HasQueryParameters { get; }

    /// <summary>
    /// Gets a value indicating whether this operation has any header parameters.
    /// </summary>
    static abstract bool HasHeaderParameters { get; }

    /// <summary>
    /// Writes the resolved path (with parameter values substituted) to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer to write the UTF-8 path to.</param>
    /// <remarks>
    /// <para>
    /// The generated implementation writes literal path segments and formatted parameter
    /// values directly, with the correct serialization style baked in. For example,
    /// a <c>simple</c> style path parameter writes the value directly after the preceding
    /// path segment.
    /// </para>
    /// <para>
    /// Only called when <see cref="HasPathParameters"/> is <see langword="true"/>.
    /// </para>
    /// </remarks>
    void WriteResolvedPath(IBufferWriter<byte> writer);

    /// <summary>
    /// Writes the query string (without the leading <c>?</c>) to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer to write the UTF-8 query string to.</param>
    /// <returns>
    /// The number of bytes written. Returns <c>0</c> if no query parameters are present
    /// (e.g. all optional parameters are unset).
    /// </returns>
    /// <remarks>
    /// <para>
    /// The generated implementation writes each parameter with its style/explode
    /// serialization baked in. The transport prepends <c>?</c> only when the return
    /// value is greater than zero.
    /// </para>
    /// </remarks>
    int WriteQueryString(IBufferWriter<byte> writer);

    /// <summary>
    /// Writes request headers by invoking the callback for each header name/value pair.
    /// </summary>
    /// <param name="callback">
    /// A callback that receives each header name and value as UTF-8 spans.
    /// The transport uses this to set headers on the underlying request.
    /// </param>
    /// <param name="state">Opaque state passed through to the callback.</param>
    /// <typeparam name="TState">The type of the state passed to the callback.</typeparam>
    /// <remarks>
    /// <para>
    /// Using a callback avoids allocating header name/value strings when the transport
    /// can set headers directly from spans (e.g. via <c>TryAddWithoutValidation</c>
    /// on <see cref="System.Net.Http.Headers.HttpRequestHeaders"/>).
    /// </para>
    /// </remarks>
    void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state);
}