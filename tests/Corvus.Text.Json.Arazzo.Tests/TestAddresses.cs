// <copyright file="TestAddresses.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Builds the composite <see cref="WorkflowRunAddress"/> (ADR 0065 decision 9) test fixtures operate runs by.
/// Every fixture in this project pins its runs to the <c>development</c> environment, so the address always
/// matches the environment the run was created with.
/// </summary>
internal static class TestAddresses
{
    /// <summary>The environment the fixtures pin their runs to.</summary>
    public const string Development = "development";

    /// <summary>Builds the address of a run pinned to the <c>development</c> environment.</summary>
    /// <param name="runId">The run id.</param>
    /// <returns>The address.</returns>
    public static WorkflowRunAddress Dev(WorkflowRunId runId) => new(Development, runId);
}