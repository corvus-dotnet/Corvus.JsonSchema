// <copyright file="TestAddresses.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Builds the composite <see cref="WorkflowRunAddress"/> (ADR 0065 decision 9) test fixtures operate runs by.
/// Most fixtures pin their runs to the <c>development</c> environment; a test that names another environment
/// builds the address explicitly so the address always matches the environment the run was created with.
/// </summary>
internal static class TestAddresses
{
    /// <summary>The environment most fixtures pin their runs to.</summary>
    public const string Development = "development";

    /// <summary>Builds the address of a run pinned to the <c>development</c> environment.</summary>
    /// <param name="runId">The run id.</param>
    /// <returns>The address.</returns>
    public static WorkflowRunAddress Dev(WorkflowRunId runId) => new(Development, runId);

    /// <summary>Builds the address of a run pinned to a named environment.</summary>
    /// <param name="environment">The environment the run is pinned to.</param>
    /// <param name="runId">The run id.</param>
    /// <returns>The address.</returns>
    public static WorkflowRunAddress In(string environment, WorkflowRunId runId) => new(environment, runId);
}