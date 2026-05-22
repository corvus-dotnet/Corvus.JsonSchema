// <copyright file="BinaryPartData.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Describes a binary part for a <c>multipart/form-data</c> request.
/// </summary>
/// <param name="WriteContentAsync">
/// An async callback that writes the binary content directly to the output stream.
/// This enables streaming from files, network resources, or other async sources
/// without requiring an intermediate in-memory buffer.
/// <para>
/// For in-memory data, use <c>(s, ct) => { s.Write(bytes); return default; }</c>.
/// </para>
/// <para>
/// For async sources (e.g. Azure Blob Storage), use
/// <c>(s, ct) => blob.DownloadToAsync(s, ct)</c>.
/// </para>
/// </param>
/// <param name="ContentType">
/// The MIME type for the part (e.g. <c>"application/octet-stream"</c>,
/// <c>"image/png"</c>).
/// </param>
/// <param name="FileName">
/// An optional filename for the <c>Content-Disposition</c> header.
/// When specified, the part header includes <c>; filename="..."</c>.
/// </param>
public readonly record struct BinaryPartData(
    Func<Stream, CancellationToken, ValueTask> WriteContentAsync,
    string ContentType = "application/octet-stream",
    string? FileName = null);