// <copyright file="Models.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Unions.Tests;

[JsonSchemaTypeGenerator("Schemas/shape.json")]
public readonly partial struct Shape;

[JsonSchemaTypeGenerator("Schemas/loose.json")]
public readonly partial struct Loose;

[JsonSchemaTypeGenerator("Schemas/not-a-union.json")]
public readonly partial struct NotAUnion;
