// <copyright file="ReceiveChannelStepEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits a <c>receive</c> AsyncAPI channel step (plan §5c): the step awaits one message on a channel via
/// the <c>ReceiveOneAsync&lt;TPayload&gt;</c> helper — a thin wrapper over the strongly-typed subscriber
/// (subscribe, capture the first message, unsubscribe) — and captures the typed payload as the step's
/// outputs (so <c>$steps.&lt;id&gt;.outputs</c> resolves against the received message).
/// </summary>
/// <remarks>
/// The helper clones the typed payload into the run's <c>JsonWorkspace</c> (the same lifetime primitive
/// used for an HTTP response body), so the outputs outlive the subscription. First increment: a
/// static-address receive that captures the whole payload as the step outputs; correlationId matching,
/// timeouts, <c>$message.*</c> criteria, and a step's explicit <c>outputs</c> projection are later phases.
/// </remarks>
internal static class ReceiveChannelStepEmitter
{
    /// <summary>
    /// Emits the statements that receive one message and capture its payload as the step's outputs.
    /// </summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="channel">The resolved channel operation.</param>
    /// <param name="messageTransportVariable">The in-scope <c>IMessageTransport</c> variable name.</param>
    /// <returns>The emitted statements (the step-outputs local is assigned, not pre-declared).</returns>
    public static string Emit(
        string stepId,
        in ResolvedChannel channel,
        string messageTransportVariable)
    {
        AsyncApiChannelDescriptor descriptor = channel.Channel;

        if (descriptor.ChannelParameters.Count > 0)
        {
            throw new NotSupportedException($"Channel step '{stepId}' receives on a parameterised channel '{descriptor.ChannelAddress}'; parameterised channel addresses are a later phase.");
        }

        // The message payload type drives the typed receive; an untyped payload schema falls back to JsonElement.
        string payloadType = descriptor.Messages.Count > 0 && descriptor.Messages[0].PayloadTypeName is { } typeName
            ? typeName
            : "Corvus.Text.Json.JsonElement";

        string outputsLocal = EmitText.StepOutputsElementLocal(stepId);

        var statements = new StringBuilder();
        statements.AppendLine("ArazzoTelemetry.StepsExecuted.Add(1);");

        // ReceiveOneAsync wraps the typed subscriber: it subscribes, captures the first typed payload
        // (cloned into the workspace, so it outlives the subscription), and unsubscribes.
        statements.Append("JsonElement ").Append(outputsLocal).Append(" = await ").Append(messageTransportVariable)
            .Append(".ReceiveOneAsync<").Append(payloadType).Append(">(").Append(EmitText.Quote(descriptor.ChannelAddress))
            .AppendLine("u8.ToArray(), workspace, cancellationToken).ConfigureAwait(false);");

        return statements.ToString();
    }
}