// <copyright file="ValidationDetail.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.Internal;

/// <summary>
/// An array of schema validation results for diagnostic output.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/validation-detail.json")]
internal readonly partial struct ValidationDetail;