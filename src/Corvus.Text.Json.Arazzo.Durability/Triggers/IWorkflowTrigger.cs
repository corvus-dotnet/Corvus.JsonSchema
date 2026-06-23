// <copyright file="IWorkflowTrigger.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A request to start a workflow run, raised by a trigger and serviced by the host's start path
/// (typically <see cref="ISecuredWorkflowManagement.StartIdempotentAsync"/>).
/// </summary>
/// <param name="WorkflowId">The versioned workflow id (<c>{base}-v{n}</c>) to run.</param>
/// <param name="Inputs">The workflow inputs.</param>
/// <param name="IdempotencyKey">A stable key identifying this logical start, so a duplicate trigger (broker redelivery, duplicate schedule fire) does not create a duplicate run.</param>
/// <param name="CorrelationId">An optional telemetry correlation id.</param>
/// <param name="Tags">Optional free-form tags to attach to the run.</param>
public readonly record struct WorkflowStartRequest(
    string WorkflowId,
    JsonElement Inputs,
    string IdempotencyKey,
    string? CorrelationId = null,
    TagSet Tags = default);

/// <summary>
/// The host's start path: creates (idempotently) and enqueues a <see cref="WorkflowRunStatus.Pending"/> run for
/// a <see cref="WorkflowStartRequest"/>, returning its id. Every trigger (HTTP, message, schedule) converges on
/// a handler of this shape.
/// </summary>
/// <param name="request">The start request.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <returns>The id of the run for this request.</returns>
public delegate ValueTask<WorkflowRunId> WorkflowStartHandler(WorkflowStartRequest request, CancellationToken cancellationToken);

/// <summary>
/// A source that initiates workflow runs — an inbound message, a schedule, an HTTP call. A trigger listens for
/// its initiating condition and, for each occurrence, invokes the host's <see cref="WorkflowStartHandler"/>.
/// </summary>
public interface IWorkflowTrigger : IAsyncDisposable
{
    /// <summary>Begins listening for the trigger's initiating condition; each occurrence starts a run via the host's start path.</summary>
    /// <param name="cancellationToken">A token that stops the trigger when cancelled.</param>
    /// <returns>A task that completes once the trigger is listening.</returns>
    ValueTask StartListeningAsync(CancellationToken cancellationToken);
}