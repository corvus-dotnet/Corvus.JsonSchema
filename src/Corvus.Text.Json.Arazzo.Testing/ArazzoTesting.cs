// <copyright file="ArazzoTesting.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Testing;

/// <summary>
/// Testing support for Arazzo workflows.
/// </summary>
/// <remarks>
/// This package provides a mock transport with response scripting, a workflow simulator for
/// deterministic dry-runs (also usable by a workflow-designer UI), and an in-memory
/// OpenTelemetry conformance harness. See <c>docs/ArazzoWorkflowEnginePlan.md</c> §3.2/§3.3.
/// </remarks>
public static class ArazzoTesting
{
    /// <summary>
    /// The default messaging-system tag used by the mock transport in telemetry assertions.
    /// </summary>
    /// <remarks>
    /// Phase 0 scaffold. The mock transport and simulator are implemented in Phase 1.
    /// </remarks>
    public const string MockMessagingSystem = "mock";
}