// <copyright file="OpenApiClientModel.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// A document's operations prepared for emission, with the document-level facts an emitter needs beside them.
/// </summary>
/// <param name="SpecVersion">The OpenAPI version of the document (<c>2.0</c>, <c>3.0</c>, <c>3.1</c> or <c>3.2</c>).</param>
/// <param name="Title">The document's <c>info.title</c>, or <see langword="null"/>.</param>
/// <param name="ApiVersion">The document's <c>info.version</c>, or <see langword="null"/>.</param>
/// <param name="RootServer">The document-level default server, or <see langword="null"/> when none is declared.</param>
/// <param name="SecuritySchemes">The security schemes the document declares, in document order.</param>
/// <param name="Operations">The prepared operations, in document order.</param>
public readonly record struct OpenApiClientModel(
    string SpecVersion,
    string? Title,
    string? ApiVersion,
    ServerInfo? RootServer,
    SecuritySchemeInfo[] SecuritySchemes,
    OperationInfo[] Operations);