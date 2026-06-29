// <copyright file="RequestBodyInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of an OpenAPI request body.
/// </summary>
/// <param name="Description">The optional description.</param>
/// <param name="IsRequired">Whether the body is required.</param>
/// <param name="Content">The content entries.</param>
/// <param name="BinaryProperties">The binary properties.</param>
/// <param name="PrefixParts">The optional ordered prefix parts (OpenAPI 3.2 sequential multipart).</param>
/// <param name="ItemPart">The optional repeating item part (OpenAPI 3.2 sequential multipart).</param>
public readonly record struct RequestBodyInfo(
    string? Description,
    bool IsRequired,
    ContentInfo[] Content,
    BinaryPropertyInfo[] BinaryProperties,
    MixedPartInfo[]? PrefixParts = null,
    MixedPartInfo? ItemPart = null);