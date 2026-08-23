// <copyright file="MultipartBinaryOrdering.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// How a streaming multipart endpoint treats the order of binary parts on the wire.
/// </summary>
public enum MultipartBinaryOrdering
{
    /// <summary>
    /// True streaming: binary parts must arrive after all non-binary parts, so the
    /// typed body is complete before the handler runs and binary content flows
    /// straight from the wire. A non-binary part after a binary part is rejected
    /// with 400. Corvus clients emit binary parts last; standard browser forms need
    /// their file inputs last or a small script to reorder the form data.
    /// </summary>
    RequireBinaryLast,

    /// <summary>
    /// Standard browser form order is accepted: a binary part arriving before the
    /// last non-binary part is spooled (to memory under the configured threshold,
    /// to a temporary file above it) and parsing continues, so the typed body always
    /// completes before the handler runs. Spooled content is cleaned up by the
    /// endpoint on every path. The trade is that out-of-order binary parts touch
    /// disk instead of streaming straight through.
    /// </summary>
    SpoolOutOfOrder,
}