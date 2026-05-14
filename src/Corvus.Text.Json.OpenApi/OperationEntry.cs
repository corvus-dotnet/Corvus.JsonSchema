// <copyright file="OperationEntry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// An operation found during spec walking, carrying the typed metadata
/// extracted from the strongly-typed schema model.
/// </summary>
/// <remarks>
/// <para>
/// The walker extracts parameter, request body, and response metadata using
/// the strongly-typed schema (e.g. <c>OpenApiDocument.ParameterOrReference</c>)
/// so that downstream consumers do not need to re-parse raw JSON.
/// </para>
/// <para>
/// The <see cref="Path"/> property name gives the path template. Use
/// <see cref="JsonProperty{TValue}.Utf8NameSpan"/> for zero-allocation access,
/// or <see cref="JsonProperty{TValue}.Name"/> when a string is needed.
/// </para>
/// </remarks>
/// <param name="Path">
/// The path property from the paths map. Provides both the path name
/// (via <see cref="JsonProperty{TValue}.Utf8NameSpan"/>) and the path item
/// value (via <see cref="JsonProperty{TValue}.Value"/>).
/// </param>
/// <param name="Operation">The operation element (castable to the typed operation struct).</param>
/// <param name="Method">The HTTP method or messaging action.</param>
/// <param name="OperationId">The operationId, or <see langword="null"/> if not declared.</param>
/// <param name="Summary">The operation summary, or <see langword="null"/> if not declared.</param>
/// <param name="Description">The operation description, or <see langword="null"/> if not declared.</param>
/// <param name="Parameters">The parameters extracted from the operation.</param>
/// <param name="RequestBody">The request body, or <see langword="null"/> if not declared.</param>
/// <param name="Responses">The responses extracted from the operation.</param>
/// <param name="Tags">The tags declared on the operation, or an empty array.</param>
public readonly record struct OperationEntry(
    JsonProperty<JsonElement> Path,
    JsonElement Operation,
    OperationMethod Method,
    string? OperationId,
    string? Summary,
    string? Description,
    WalkedParameter[] Parameters,
    WalkedRequestBody? RequestBody,
    WalkedResponse[] Responses,
    string[] Tags);