// <copyright file="ArazzoDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo11;

/// <summary>
/// An Arazzo 1.1 workflow document, as defined by https://spec.openapis.org/arazzo/v1.1.
/// </summary>
/// <remarks>
/// The 1.1 model is generated from a schema derived from the official 1.0 schema plus the
/// documented 1.1.0 additions ($self, AsyncAPI source/step fields, querystring parameters);
/// the OpenAPI Initiative has not published an official 1.1 JSON Schema.
/// </remarks>
[JsonSchemaTypeGenerator("Arazzo11.json")]
public readonly partial struct ArazzoDocument;