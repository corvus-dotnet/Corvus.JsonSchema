// <copyright file="AccessApprovalInputs.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows;

/// <summary>
/// The typed inputs the control plane starts a run of the system access-approval workflow with (design §16.5.1),
/// generated from <c>Schemas/access-approval-inputs.json</c>. Built with <c>CreateBuilder</c> by copying the pending
/// request's JSON values, so the run inputs are assembled with no per-field string realised.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/access-approval-inputs.json")]
public readonly partial struct AccessApprovalInputs;