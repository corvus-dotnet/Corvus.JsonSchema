// <copyright file="ContentEncodingInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// A per-property <c>encoding</c> entry of a form or multipart body.
/// </summary>
/// <param name="Style">The <c>style</c>, or <see langword="null"/> for the default.</param>
/// <param name="Explode">The <c>explode</c> setting, or <see langword="null"/> for the default.</param>
/// <param name="AllowReserved">Whether reserved characters may appear unescaped (<c>allowReserved</c>).</param>
/// <param name="ContentType">The property's content type, or <see langword="null"/> for the default.</param>
public readonly record struct ContentEncodingInfo(
    string? Style,
    bool? Explode,
    bool AllowReserved,
    string? ContentType);