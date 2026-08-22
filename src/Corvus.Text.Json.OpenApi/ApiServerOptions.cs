// <copyright file="ApiServerOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Registration-time options for generated server endpoints, accepted by the generated
/// <c>MapApiEndpoints</c> extension methods.
/// </summary>
public sealed class ApiServerOptions
{
    /// <summary>
    /// The default value of <see cref="MaxBufferedRequestBodyLength"/>: 128 MiB, matching
    /// the default multipart body length limit of ASP.NET Core's form binding.
    /// </summary>
    public const long DefaultMaxBufferedRequestBodyLength = 134_217_728;

    /// <summary>
    /// Gets the maximum number of bytes a request body may contain on the body paths that
    /// buffer the whole body before the handler runs (<c>multipart/form-data</c>,
    /// <c>multipart/mixed</c> and <c>application/x-www-form-urlencoded</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bodies over the limit are rejected with a 413 Payload Too Large response. Raw stream
    /// bodies and JSON bodies are not buffered by this layer and are not affected; cap those
    /// with the web server's request size limit (for example Kestrel's
    /// <c>MaxRequestBodySize</c>).
    /// </para>
    /// </remarks>
    public long MaxBufferedRequestBodyLength { get; init; } = DefaultMaxBufferedRequestBodyLength;
}