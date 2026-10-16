// <copyright file="RequestBodyInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// A request body prepared for emission.
/// </summary>
/// <param name="Description">The body's <c>description</c>, or <see langword="null"/>.</param>
/// <param name="IsRequired">Whether the body is required.</param>
/// <param name="Content">The body's media types, in document order.</param>
/// <param name="BinaryProperties">For a multipart body, the properties that carry binary parts; otherwise empty.</param>
/// <param name="PrefixParts">For an OpenAPI 3.2 <c>multipart/mixed</c> body, the positional prefix parts; otherwise <see langword="null"/>.</param>
/// <param name="ItemPart">For an OpenAPI 3.2 <c>multipart/mixed</c> body, the repeated item part; otherwise <see langword="null"/>.</param>
public readonly record struct RequestBodyInfo(
    string? Description,
    bool IsRequired,
    ContentInfo[] Content,
    BinaryPropertyInfo[] BinaryProperties,
    MixedPartInfo[]? PrefixParts = null,
    MixedPartInfo? ItemPart = null);