// <copyright file="OpenApiDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi30;

/// <summary>
/// An OpenAPI 3.0 document, as defined by https://spec.openapis.org/oas/v3.0.3.
/// </summary>
[JsonSchemaTypeGenerator("OpenApi30.json")]
public readonly partial struct OpenApiDocument;