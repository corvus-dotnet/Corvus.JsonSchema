// <copyright file="WorkflowExecutorFault.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The error type recorded on a run whose executor failed in a way nothing in the workflow handled.
/// </summary>
public static class WorkflowExecutorFault
{
    /// <summary>
    /// The executor threw something that was not a step failure, a pause, a budget fault or a credential fault.
    /// </summary>
    /// <remarks>
    /// The fault carries this fixed value and not the exception's message. A run record is read by whoever may see the
    /// run, and an exception message can name hosts, paths and internals that reader has no business with. The
    /// exception itself goes to the executor's activity, where the operator's telemetry has it.
    /// </remarks>
    public const string Unhandled = "executor-unhandled";
}