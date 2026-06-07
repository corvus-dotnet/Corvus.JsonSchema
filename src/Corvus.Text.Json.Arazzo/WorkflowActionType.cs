// <copyright file="WorkflowActionType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// The type of an Arazzo Success/Failure Action. <see cref="Retry"/> applies to failure actions only.
/// </summary>
public enum WorkflowActionType
{
    /// <summary>End the workflow (success: complete; failure: fault/return).</summary>
    End,

    /// <summary>Transfer control to another step or workflow.</summary>
    Goto,

    /// <summary>Retry the current step (failure actions only).</summary>
    Retry,
}