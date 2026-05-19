// <copyright file="ContentCategory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Classifies a media type for code generation purposes.
/// </summary>
/// <remarks>
/// <para>
/// This determines how request bodies are sent and response bodies are parsed
/// in the generated client code. The classification also handles wildcard
/// media type ranges: <c>text/*</c> maps to <see cref="TextPlain"/>,
/// while <c>*/*</c> and <c>application/*</c> map to <see cref="OctetStream"/>.
/// </para>
/// </remarks>
public enum ContentCategory
{
    /// <summary>
    /// JSON content (<c>application/json</c> or any <c>+json</c> structured syntax suffix).
    /// Request bodies are serialized via the typed JSON element.
    /// Response bodies are parsed into strongly-typed schema models.
    /// </summary>
    Json,

    /// <summary>
    /// Binary stream content (<c>application/octet-stream</c>, <c>*/*</c>,
    /// <c>application/*</c>, <c>image/*</c>, etc.).
    /// Request bodies are sent as raw <see cref="System.IO.Stream"/>.
    /// Response bodies are exposed as raw <see cref="System.IO.Stream"/>.
    /// </summary>
    OctetStream,

    /// <summary>
    /// Plain text content (<c>text/plain</c>, <c>text/*</c>).
    /// Request bodies are sent as UTF-8 encoded strings.
    /// Response bodies are read as strings.
    /// </summary>
    TextPlain,

    /// <summary>
    /// Form URL-encoded content (<c>application/x-www-form-urlencoded</c>).
    /// Request bodies are serialized as URL-encoded key=value pairs from
    /// the schema object's properties (OAS §4.8.14.4).
    /// </summary>
    FormUrlEncoded,

    /// <summary>
    /// Multipart form data content (<c>multipart/form-data</c>).
    /// Request bodies are serialized as MIME multipart parts, one per
    /// schema property (OAS §4.8.14.5).
    /// </summary>
    Multipart,

    /// <summary>
    /// Multipart mixed content (<c>multipart/mixed</c>).
    /// Request bodies are serialized as positional MIME parts using
    /// <c>prefixEncoding</c> (per-position) or <c>itemEncoding</c> (uniform).
    /// OAS 3.2 §4.8.14.
    /// </summary>
    MultipartMixed,
}