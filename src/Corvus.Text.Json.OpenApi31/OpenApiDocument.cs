// <copyright file="OpenApiDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi31;

/// <summary>
/// An OpenAPI 3.1 document, as defined by https://spec.openapis.org/oas/v3.1.0.
/// </summary>
[JsonSchemaTypeGenerator("OpenApi31.json")]
public readonly partial struct OpenApiDocument;