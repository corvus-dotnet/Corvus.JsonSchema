// <copyright file="JsonPatchDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Patch;

/// <summary>
/// An RFC 6902 JSON Patch document.
/// </summary>
[JsonSchemaTypeGenerator("json-patch.json")]
public readonly partial struct JsonPatchDocument;