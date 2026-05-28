// <copyright file="IResponseHeaders.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Provides read access to response headers from a transport.
/// </summary>
/// <remarks>
/// <para>
/// This interface decouples the generated response types from any specific
/// transport implementation. The <c>HttpClientTransport</c> provides
/// an adapter that wraps <c>HttpResponseMessage.Headers</c>.
/// </para>
/// </remarks>
public interface IResponseHeaders
{
    /// <summary>
    /// Tries to get a single header value by name.
    /// </summary>
    /// <param name="headerName">The header name (case-insensitive per HTTP semantics).</param>
    /// <param name="value">The header value, or <see langword="null"/> if not found.</param>
    /// <returns><see langword="true"/> if the header was present.</returns>
    /// <remarks>
    /// <para>
    /// If the header has multiple values, the first value is returned.
    /// </para>
    /// </remarks>
    bool TryGetValue(string headerName, out string? value);
}