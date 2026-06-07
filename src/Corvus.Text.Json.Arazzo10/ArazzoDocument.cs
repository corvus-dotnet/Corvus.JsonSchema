// <copyright file="ArazzoDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo10;

/// <summary>
/// An Arazzo 1.0 workflow document, as defined by https://spec.openapis.org/arazzo/v1.0.
/// </summary>
[JsonSchemaTypeGenerator("Arazzo10.json")]
public readonly partial struct ArazzoDocument;