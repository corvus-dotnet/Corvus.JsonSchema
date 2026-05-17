// <copyright file="PropertyEncoding.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Describes per-property encoding overrides for form-urlencoded and multipart
/// request bodies, as defined by the OpenAPI Encoding Object (§4.8.14.5).
/// </summary>
/// <param name="Style">
/// The serialization style for array and object values.
/// Valid values: <c>"form"</c> (default), <c>"spaceDelimited"</c>,
/// <c>"pipeDelimited"</c>, <c>"deepObject"</c>.
/// Only meaningful for <c>application/x-www-form-urlencoded</c> and
/// non-binary <c>multipart/form-data</c> parts.
/// </param>
/// <param name="Explode">
/// Whether arrays and objects produce separate key=value pairs.
/// When <see langword="null"/>, the default applies:
/// <see langword="true"/> for <c>form</c> style,
/// <see langword="false"/> for all other styles.
/// </param>
/// <param name="AllowReserved">
/// When <see langword="true"/>, reserved characters (<c>:/?#[]@!$&amp;'()*+,;=</c>)
/// are not percent-encoded. Default is <see langword="false"/>.
/// Only applies to <c>application/x-www-form-urlencoded</c>.
/// </param>
/// <param name="ContentType">
/// Overrides the <c>Content-Type</c> for a multipart part.
/// Only applies to <c>multipart/form-data</c>.
/// </param>
public readonly record struct PropertyEncoding(
    string? Style = null,
    bool? Explode = null,
    bool AllowReserved = false,
    string? ContentType = null)
{
    /// <summary>
    /// Gets the effective explode value, applying the OAS default:
    /// <see langword="true"/> when <see cref="Style"/> is <c>"form"</c> or
    /// <see langword="null"/>; <see langword="false"/> otherwise.
    /// </summary>
    public bool EffectiveExplode
        => this.Explode ?? (this.Style is null or "form");
}