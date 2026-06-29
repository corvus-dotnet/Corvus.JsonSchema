// <copyright file="EncodingInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of an OpenAPI encoding entry.
/// </summary>
/// <param name="Style">The optional serialization style.</param>
/// <param name="Explode">The optional explode flag.</param>
/// <param name="AllowReserved">Whether reserved characters are permitted unescaped.</param>
/// <param name="ContentType">The optional content type.</param>
public readonly record struct EncodingInfo(
    string? Style,
    bool? Explode,
    bool AllowReserved,
    string? ContentType);