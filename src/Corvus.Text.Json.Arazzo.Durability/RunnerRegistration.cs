// <copyright file="RunnerRegistration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A registration record for a workflow runner process: its identity, liveness
/// timestamps, capacity, supported transports, and the catalog versions it hosts.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/RunnerRegistration.json")]
public readonly partial struct RunnerRegistration;