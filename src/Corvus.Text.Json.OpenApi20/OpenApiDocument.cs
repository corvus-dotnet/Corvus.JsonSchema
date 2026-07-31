// <copyright file="OpenApiDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi20;

/// <summary>
/// An OpenAPI 2.0 (Swagger) document, as defined by https://swagger.io/specification/v2/.
/// </summary>
[JsonSchemaTypeGenerator("OpenApi20.json")]
public readonly partial struct OpenApiDocument;