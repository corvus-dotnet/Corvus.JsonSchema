// <copyright file="DeploymentBootstrapOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap;

/// <summary>
/// Strongly-typed configuration for <see cref="IDeploymentBootstrap"/>, generated from
/// <c>deployment-bootstrap-options.json</c> by the Corvus.Text.Json source generator. Every value that is specific
/// to a deployment (the genesis-administrator group, the capability scopes it holds, the label-ordering taxonomy,
/// the identity claim, …) lives here, so the bootstrap logic itself is generic. It is a JSON value: a deployment
/// binds it from configuration — ZeroFailed, appsettings, an env-injected blob, a secret store — validated against
/// the schema, rather than hand-authoring a C# options record.
/// </summary>
[JsonSchemaTypeGenerator("deployment-bootstrap-options.json")]
public readonly partial struct DeploymentBootstrapOptions;