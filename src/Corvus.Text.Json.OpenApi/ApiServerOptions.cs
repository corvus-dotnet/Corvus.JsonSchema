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

    /// <summary>
    /// Gets the ordering policy for binary parts on streaming multipart endpoints
    /// (servers generated with <c>--serverBinaryParts stream</c>). Buffered endpoints
    /// ignore this setting.
    /// </summary>
    public MultipartBinaryOrdering MultipartBinaryOrdering { get; init; } = MultipartBinaryOrdering.RequireBinaryLast;

    /// <summary>
    /// Gets the maximum total bytes of non-binary (text and JSON) parts a streaming
    /// multipart endpoint accumulates for the typed body. Bodies over the limit are
    /// rejected with 413. Binary parts are unlimited; they stream. Buffered endpoints
    /// ignore this setting.
    /// </summary>
    public long MaxNonBinaryPartsLength { get; init; } = 1_048_576;

    /// <summary>
    /// The default value of <see cref="SpoolMemoryThresholdBytes"/>: 64 KiB.
    /// </summary>
    public const int DefaultSpoolMemoryThresholdBytes = 65_536;

    /// <summary>
    /// Gets the size at or below which a binary part spooled under
    /// <see cref="MultipartBinaryOrdering.SpoolOutOfOrder"/> is held in pooled memory.
    /// Larger parts spool to a temporary file that is deleted when the request
    /// completes. Ignored under <see cref="MultipartBinaryOrdering.RequireBinaryLast"/>
    /// and by buffered endpoints.
    /// </summary>
    public int SpoolMemoryThresholdBytes { get; init; } = DefaultSpoolMemoryThresholdBytes;

    /// <summary>
    /// Gets the directory in which binary part spool files are created under
    /// <see cref="MultipartBinaryOrdering.SpoolOutOfOrder"/>, or <see langword="null"/>
    /// to use the system temporary directory.
    /// </summary>
    public string? SpoolDirectory { get; init; }

    /// <summary>
    /// Gets the maximum total bytes of binary parts spooled per request under
    /// <see cref="MultipartBinaryOrdering.SpoolOutOfOrder"/>. Requests over the limit
    /// are rejected with 413. The default is unbounded: large uploads are the point of
    /// spooling, and the web server's request size limit (for example Kestrel's
    /// <c>MaxRequestBodySize</c>) governs; tighten this to bound spool disk usage
    /// independently of that limit.
    /// </summary>
    public long MaxSpooledBodyLength { get; init; } = long.MaxValue;
}