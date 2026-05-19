// <copyright file="OpenApiDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi32;

/// <summary>
/// An OpenAPI 3.2 document, as defined by https://spec.openapis.org/oas/v3.2.
/// </summary>
[JsonSchemaTypeGenerator("OpenApi32.json")]
public readonly partial struct OpenApiDocument;