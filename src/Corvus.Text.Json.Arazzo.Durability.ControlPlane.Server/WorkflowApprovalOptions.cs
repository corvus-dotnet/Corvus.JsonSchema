// <copyright file="WorkflowApprovalOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.AsyncApi;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Opts a control-plane deployment into the workflow-backed access-request approval strategy (design §16.5.1): Arazzo
/// governs its own approvals. When supplied to <see cref="ControlPlaneEndpointExtensions.MapArazzoControlPlane"/>, the
/// access-request surface is backed by <see cref="WorkflowBackedAccessRequestApprovalService"/> instead of the built-in
/// direct-to-administrator strategy — submitting a request starts a run of the bootstrapped approval workflow, and the
/// approve/reject/withdraw touchpoints publish the decision onto the <c>access.decision</c> channel.
/// </summary>
/// <remarks>
/// The approval workflow must be installed in the catalog and made available in <see cref="Environment"/>, a system
/// runner must serve that environment and host the <c>access.decision</c> consumer, and the workflow's §13 system
/// credential must hold the <c>accessRequests:grant</c> capability. Absent these the strategy still routes decisions
/// correctly but the runs will not advance.
/// </remarks>
public sealed class WorkflowApprovalOptions
{
    /// <summary>Gets the transport the approver's decision is published on (the same transport the system runner's decision consumer subscribes to).</summary>
    public required IMessageTransport DecisionTransport { get; init; }

    /// <summary>Gets the versioned id of the bootstrapped approval workflow to start on submit. Defaults to <c>access-approval-v1</c>.</summary>
    public string ApprovalWorkflowId { get; init; } = "access-approval-v1";

    /// <summary>Gets the control-plane internal environment the approval run executes in (served by the system runner). Defaults to <c>system</c>.</summary>
    public string Environment { get; init; } = "system";
}