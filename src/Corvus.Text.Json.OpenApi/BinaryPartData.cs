// <copyright file="BinaryPartData.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Describes a binary part for a <c>multipart/form-data</c> request.
/// </summary>
/// <param name="Content">The binary content of the part.</param>
/// <param name="ContentType">
/// The MIME type for the part (e.g. <c>"application/octet-stream"</c>,
/// <c>"image/png"</c>).
/// </param>
/// <param name="FileName">
/// An optional filename for the <c>Content-Disposition</c> header.
/// When specified, the part header includes <c>; filename="..."</c>.
/// </param>
public readonly record struct BinaryPartData(
    ReadOnlyMemory<byte> Content,
    string ContentType = "application/octet-stream",
    string? FileName = null);