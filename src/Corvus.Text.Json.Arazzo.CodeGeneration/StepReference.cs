// <copyright file="StepReference.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// The kind of target a step invokes.
/// </summary>
public enum StepTargetKind
{
    /// <summary>The step has no recognized target.</summary>
    None,

    /// <summary>The step invokes an operation by <c>operationId</c>.</summary>
    OperationId,

    /// <summary>The step invokes an operation by <c>operationPath</c>.</summary>
    OperationPath,

    /// <summary>The step invokes another workflow by <c>workflowId</c>.</summary>
    WorkflowId,
}

/// <summary>
/// A reference made by a workflow step to an operation or sub-workflow.
/// </summary>
/// <param name="WorkflowId">The id of the workflow that contains the step.</param>
/// <param name="StepId">The step id.</param>
/// <param name="Kind">The kind of target.</param>
/// <param name="Target">The target value (operationId / operationPath / workflowId), or <see langword="null"/>.</param>
public readonly record struct StepReference(string WorkflowId, string StepId, StepTargetKind Kind, string? Target);