// <copyright file="ArazzoSpecificationVersion.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Well-known Arazzo Specification versions supported by the workflow execution engine.
/// </summary>
/// <remarks>
/// This is the entry point for the Arazzo runtime support library. The execution context,
/// runtime-expression evaluator, criterion evaluator, and control-flow primitives are added
/// in subsequent phases (see <c>docs/ArazzoWorkflowEnginePlan.md</c>).
/// </remarks>
public static class ArazzoSpecificationVersion
{
    /// <summary>
    /// Arazzo 1.0.x, as defined by https://spec.openapis.org/arazzo/v1.0.
    /// </summary>
    public const string V10 = "1.0";
}