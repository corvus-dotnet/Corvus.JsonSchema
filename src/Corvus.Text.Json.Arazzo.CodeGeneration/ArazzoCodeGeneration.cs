// <copyright file="ArazzoCodeGeneration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Generates strongly-typed workflow executors from Arazzo documents.
/// </summary>
/// <remarks>
/// The generator walks an Arazzo document together with its resolved source descriptions,
/// computes the exact set of referenced operations (so only those are generated — see
/// <c>docs/ArazzoWorkflowEnginePlan.md</c> §3.1), maps each step to the corresponding generated
/// OpenAPI/AsyncAPI request/response types, and emits one executor class per workflow.
/// </remarks>
public static class ArazzoCodeGeneration
{
    /// <summary>
    /// The default .NET namespace suffix for generated workflow executors.
    /// </summary>
    /// <remarks>
    /// Phase 0 scaffold. The generator is implemented from Phase 2 onwards.
    /// </remarks>
    public const string DefaultWorkflowsNamespaceSuffix = "Workflows";
}