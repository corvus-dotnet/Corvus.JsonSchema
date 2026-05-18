// <copyright file="OpenApiLockFileModel.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Strongly-typed model for the corvusjson-openapi.lock file, generated
/// from the lock-file JSON Schema.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/corvusjson-openapi-lock.json")]
public readonly partial struct OpenApiLockFileModel;