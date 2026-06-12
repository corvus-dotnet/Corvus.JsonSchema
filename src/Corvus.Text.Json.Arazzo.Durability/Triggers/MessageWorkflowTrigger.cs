// <copyright file="MessageWorkflowTrigger.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The host configuration that binds a workflow to an inbound message channel: an event on
/// <see cref="Channel"/> starts a run of <see cref="WorkflowId"/>. The channel and the payload→inputs /
/// idempotency mapping are deliberately host config (not baked into the Arazzo document) so ops can bind the
/// same workflow differently per environment.
/// </summary>
/// <param name="WorkflowId">The versioned workflow id (<c>{base}-v{n}</c>) to start.</param>
/// <param name="Channel">The start channel address to subscribe to.</param>
/// <param name="IdempotencyKey">Derives the idempotency key for a message (e.g. read a message-id header), so a redelivered message does not start a duplicate run.</param>
/// <param name="MapInputs">Maps the message payload (and headers) to the workflow inputs; when <see langword="null"/>, the payload is used as the inputs.</param>
/// <param name="CorrelationId">Optionally derives a telemetry correlation id from the message.</param>
public sealed record MessageTriggerBinding(
    string WorkflowId,
    string Channel,
    Func<JsonElement, JsonElement, string> IdempotencyKey,
    Func<JsonElement, JsonElement, JsonElement>? MapInputs = null,
    Func<JsonElement, JsonElement, string?>? CorrelationId = null);

/// <summary>
/// A <see cref="IWorkflowTrigger"/> that starts a workflow run for each inbound message on a configured channel.
/// It is the runner-side counterpart of the control plane's HTTP trigger: an event initiates a fresh run
/// (distinct from a Tier-2 resume, which wakes an already-suspended run). Idempotency keying off the message
/// avoids duplicate runs on broker redelivery.
/// </summary>
public sealed class MessageWorkflowTrigger : IWorkflowTrigger
{
    private readonly IMessageTransport transport;
    private readonly WorkflowStartHandler start;
    private readonly MessageTriggerBinding binding;

    // The channel bytes are retained for the lifetime of the subscription (subscribe/unsubscribe), so they are
    // owned here rather than pool-rented.
    private byte[]? channelUtf8;

    /// <summary>Initializes a new instance of the <see cref="MessageWorkflowTrigger"/> class.</summary>
    /// <param name="transport">The message transport to subscribe on.</param>
    /// <param name="start">The host start path each message invokes.</param>
    /// <param name="binding">The channel + mapping binding.</param>
    public MessageWorkflowTrigger(IMessageTransport transport, WorkflowStartHandler start, MessageTriggerBinding binding)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(binding);
        this.transport = transport;
        this.start = start;
        this.binding = binding;
    }

    /// <inheritdoc/>
    public ValueTask StartListeningAsync(CancellationToken cancellationToken)
    {
        this.channelUtf8 = Encoding.UTF8.GetBytes(this.binding.Channel);
        return this.transport.SubscribeAsync<JsonElement>(this.channelUtf8, this.OnMessageAsync, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
        => this.channelUtf8 is { } channel ? this.transport.UnsubscribeAsync(channel, default) : ValueTask.CompletedTask;

    private async ValueTask OnMessageAsync(JsonElement payload, JsonElement headers, CancellationToken cancellationToken)
    {
        string idempotencyKey = this.binding.IdempotencyKey(payload, headers);
        JsonElement inputs = this.binding.MapInputs is { } map ? map(payload, headers) : payload;
        string? correlationId = this.binding.CorrelationId?.Invoke(payload, headers);

        var request = new WorkflowStartRequest(this.binding.WorkflowId, inputs, idempotencyKey, correlationId);
        await this.start(request, cancellationToken).ConfigureAwait(false);
    }
}