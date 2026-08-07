// <copyright file="ControlPlaneCapacityNames.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Capacity;

/// <summary>
/// The names a capacity refusal carries. Declared as constants rather than composed per refusal: they are part of the
/// API's observable contract, so a client may match on them, and building them by concatenation would make a rename
/// silent.
/// </summary>
public static class ControlPlaneCapacityNames
{
    /// <summary>The problem type every capacity refusal carries.</summary>
    public const string ProblemType = "https://corvus-oss.org/arazzo/control-plane/problems/capacity-exceeded";

    /// <summary>The counter reported when a magnitude is measured against the deployment rather than a named owner group.</summary>
    public const string Deployment = "deployment";

    /// <summary>The name of the limit for one magnitude.</summary>
    /// <param name="kind">The magnitude.</param>
    /// <returns>The limit's name, in the same "dimension/scope" shape the runner API's quota names use.</returns>
    public static string Of(ControlPlaneCapacityKind kind) => kind switch
    {
        ControlPlaneCapacityKind.ConcurrentRuns => "concurrent-runs/tenant",
        ControlPlaneCapacityKind.StoredRuns => "run-count/tenant",
        ControlPlaneCapacityKind.RegisteredRunners => "registered-runners/environment",
        _ => "unknown",
    };
}