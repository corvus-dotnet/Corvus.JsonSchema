// <copyright file="AsyncApiLockFileModel.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Strongly-typed model for the corvusjson-asyncapi.lock file, generated
/// from the lock-file JSON Schema.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/corvusjson-asyncapi-lock.json")]
public readonly partial struct AsyncApiLockFileModel;