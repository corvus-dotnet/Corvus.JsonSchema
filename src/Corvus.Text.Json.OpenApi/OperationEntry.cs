// <copyright file="OperationEntry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// An operation found during spec walking, holding element references into the
/// parsed document.
/// </summary>
/// <remarks>
/// <para>
/// All element references are valid only while the source document is alive.
/// Use <see cref="JsonProperty{TValue}.Utf8NameSpan"/> on <see cref="Path"/>
/// to read the path name without allocation, or <see cref="JsonProperty{TValue}.Name"/>
/// when a string is needed at a display boundary.
/// </para>
/// <para>
/// The <see cref="Operation"/> element can be cast to the appropriate typed
/// operation struct (e.g., <c>OpenApiDocument.Operation</c>) for typed access
/// to <c>OperationId</c>, <c>Summary</c>, <c>RequestBody</c>, etc.
/// </para>
/// </remarks>
/// <param name="Path">
/// The path property from the paths map. Provides both the path name
/// (via <see cref="JsonProperty{TValue}.Utf8NameSpan"/>) and the path item
/// value (via <see cref="JsonProperty{TValue}.Value"/>).
/// </param>
/// <param name="Operation">The operation element.</param>
/// <param name="Method">The HTTP method or messaging action.</param>
public readonly record struct OperationEntry(
    JsonProperty<JsonElement> Path,
    JsonElement Operation,
    OperationMethod Method);