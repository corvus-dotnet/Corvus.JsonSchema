// <copyright file="MessageWorkflowTrigger.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.AsyncApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The host configuration that binds a workflow to an inbound message channel: an event on
/// <see cref="Channel"/> starts a run of <see cref="WorkflowId"/>. The channel and the payload→inputs /
/// idempotency mapping are deliberately host config (not baked into the Arazzo document) so ops can bind the
/// same workflow differently per environment.
/// </summary>
/// <remarks>
/// The mappings are <b>Arazzo runtime expressions</b> over the message — the same vocabulary the executor
/// already uses (<c>$message.payload#/&lt;pointer&gt;</c>, <c>$message.header.&lt;name&gt;</c>), or a literal.
/// This keeps the binding plain, serializable data (so it can move into a declared-trigger manifest later —
/// design §6.5) and reuses <see cref="WorkflowExecutionContext"/> to evaluate it, rather than carrying C#
/// delegates.
/// </remarks>
/// <param name="WorkflowId">The versioned workflow id (<c>{base}-v{n}</c>) to start.</param>
/// <param name="Channel">The start channel address to subscribe to.</param>
/// <param name="IdempotencyKey">A runtime expression selecting the idempotency key from the message (e.g. <c>$message.payload#/orderId</c> or <c>$message.header.message-id</c>), so a redelivered message does not start a duplicate run.</param>
/// <param name="Inputs">A runtime expression selecting the workflow inputs from the message; when <see langword="null"/>, the whole payload is used as the inputs.</param>
/// <param name="CorrelationId">An optional runtime expression selecting a telemetry correlation id from the message.</param>
public sealed record MessageTriggerBinding(
    string WorkflowId,
    string Channel,
    string IdempotencyKey,
    string? Inputs = null,
    string? CorrelationId = null);

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

    // The binding's selectors, parsed once. Plain-data string expressions in, runtime-evaluable expressions out.
    private readonly ArazzoExpression idempotencyKey;
    private readonly ArazzoExpression? inputs;
    private readonly ArazzoExpression? correlationId;

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
        this.idempotencyKey = ArazzoExpression.Parse(binding.IdempotencyKey);
        this.inputs = binding.Inputs is { } i ? ArazzoExpression.Parse(i) : null;
        this.correlationId = binding.CorrelationId is { } c ? ArazzoExpression.Parse(c) : null;
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
        // Seed a context with the message so the binding's runtime expressions resolve against it, exactly as
        // the executor resolves $message.* during a run.
        var context = new WorkflowExecutionContext();
        context.SetMessagePayload(payload);
        if (headers.ValueKind == JsonValueKind.Object)
        {
            foreach (var header in headers.EnumerateObject())
            {
                if (header.Value.ValueKind == JsonValueKind.String)
                {
                    context.SetMessageHeader(header.Name.ToString(), header.Value.GetString()!);
                }
            }
        }

        string key = context.TryResolveString(this.idempotencyKey, out string resolvedKey) ? resolvedKey : string.Empty;
        JsonElement runInputs = this.inputs is { } inputExpr && context.TryResolveValue(inputExpr, out JsonElement mapped) ? mapped : payload;
        string? correlation = this.correlationId is { } correlationExpr && context.TryResolveString(correlationExpr, out string resolvedCorrelation) ? resolvedCorrelation : null;

        var request = new WorkflowStartRequest(this.binding.WorkflowId, runInputs, key, correlation);
        await this.start(request, cancellationToken).ConfigureAwait(false);
    }
}