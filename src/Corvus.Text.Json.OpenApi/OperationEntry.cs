// <copyright file="OperationEntry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// An operation found during spec walking, carrying element references
/// and computed metadata extracted from the strongly-typed schema model.
/// </summary>
/// <remarks>
/// <para>
/// No strings are allocated. The <see cref="Path"/> property gives the
/// path template via <see cref="JsonProperty{TValue}.Utf8NameSpan"/> or
/// <see cref="JsonProperty{TValue}.Name"/>. The <see cref="Operation"/>
/// element provides access to operationId, summary, description, and tags.
/// </para>
/// <para>
/// The walker uses typed schemas (e.g. <c>OpenApiDocument.ParameterOrReference</c>)
/// to compute <see cref="ParameterLocation"/>, <see cref="ParameterStyle"/>,
/// and explode values on <see cref="WalkedParameter"/>.
/// </para>
/// </remarks>
/// <param name="Path">
/// The path property from the paths map. Provides both the path name
/// (via <see cref="JsonProperty{TValue}.Utf8NameSpan"/>) and the path item
/// value (via <see cref="JsonProperty{TValue}.Value"/>).
/// </param>
/// <param name="Operation">The operation element (castable to the typed operation struct).</param>
/// <param name="Method">The HTTP method or messaging action.</param>
/// <param name="Parameters">The parameters extracted from the operation.</param>
/// <param name="RequestBody">The request body, or <see langword="null"/> if not declared.</param>
/// <param name="Responses">The responses extracted from the operation.</param>
public readonly record struct OperationEntry(
    JsonProperty<JsonElement> Path,
    JsonElement Operation,
    OperationMethod Method,
    WalkedParameter[] Parameters,
    WalkedRequestBody? RequestBody,
    WalkedResponse[] Responses);