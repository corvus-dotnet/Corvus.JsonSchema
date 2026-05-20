// <copyright file="TagInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Metadata for a top-level tag defined in the OpenAPI specification.
/// </summary>
/// <param name="Name">The tag name (matches values used in operation <c>tags</c> arrays).</param>
/// <param name="Summary">A short display name for the tag, or <see langword="null"/>.</param>
/// <param name="Description">A longer description of the tag, or <see langword="null"/>.</param>
/// <param name="Parent">The <c>name</c> of the parent tag (for hierarchical organization), or <see langword="null"/>.</param>
/// <param name="Kind">A classification string (e.g. <c>nav</c>, <c>badge</c>, <c>audience</c>), or <see langword="null"/>.</param>
public readonly record struct TagInfo(
    string Name,
    string? Summary,
    string? Description,
    string? Parent,
    string? Kind);