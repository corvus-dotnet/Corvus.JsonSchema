// <copyright file="Person.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace V5Example.Model;

/// <summary>
/// A person, generated from person.json schema.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/person.json")]
public readonly partial struct Person;
