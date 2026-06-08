// <copyright file="ReceiveChannelStepEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits a <c>receive</c> AsyncAPI channel step (plan §5c): the step awaits one message on a channel via
/// the <c>ReceiveOneAsync&lt;TPayload&gt;</c> helper — a thin wrapper over the strongly-typed subscriber
/// (subscribe, capture the first message, unsubscribe) — and exposes the received message as the step's
/// outputs (so <c>$steps.&lt;id&gt;.outputs</c> resolves against it).
/// </summary>
/// <remarks>
/// When the step declares <c>outputs</c> they are projected from the received payload via
/// <see cref="OutputExtractionEmitter"/>: a <c>$message.payload[#/ptr]</c> expression navigates the
/// received message and copies only the addressed value into the step's outputs object. When the step
/// declares no outputs the whole received message becomes the step's outputs (the original behaviour).
/// The helper clones the typed payload into the run's <c>JsonWorkspace</c> (the same lifetime primitive
/// used for an HTTP response body). <c>$message.header.*</c>, correlationId matching, timeouts, and
/// parameterised addresses are later phases.
/// </remarks>
internal static class ReceiveChannelStepEmitter
{
    /// <summary>
    /// Emits the fields and statements that receive one message and expose it as the step's outputs.
    /// </summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="channel">The resolved channel operation.</param>
    /// <param name="messageTransportVariable">The in-scope <c>IMessageTransport</c> variable name.</param>
    /// <param name="outputs">The step's declared outputs (name → runtime expression); empty for whole-payload capture.</param>
    /// <param name="workspaceVariable">The in-scope <c>JsonWorkspace</c> variable name.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputsVariable">The workflow inputs variable name (for <c>$inputs</c> navigation in outputs).</param>
    /// <param name="inputAccessors">The input accessor map, or <see langword="null"/> for untyped inputs.</param>
    /// <returns>The emitted fields and statements (the step-outputs element local is declared and assigned).</returns>
    public static ReceiveChannelStepCode Emit(
        string stepId,
        in ResolvedChannel channel,
        string messageTransportVariable,
        IReadOnlyList<OutputMapping> outputs,
        string workspaceVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors)
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

        string outputsElementLocal = EmitText.StepOutputsElementLocal(stepId);

        var fields = new StringBuilder();
        var statements = new StringBuilder();
        statements.AppendLine("ArazzoTelemetry.StepsExecuted.Add(1);");

        if (outputs.Count == 0)
        {
            // No declared outputs: the whole received message is the step's outputs.
            statements.Append("JsonElement ").Append(outputsElementLocal);
            AppendReceive(statements, messageTransportVariable, payloadType, descriptor.ChannelAddress, workspaceVariable);
            return new ReceiveChannelStepCode(fields.ToString(), statements.ToString());
        }

        // Declared outputs: receive the message, then project only the declared values from it.
        string payloadLocal = $"{EmitText.ToCamelCase(EmitText.SanitizeIdentifier(stepId))}MessagePayload";
        statements.Append("JsonElement ").Append(payloadLocal);
        AppendReceive(statements, messageTransportVariable, payloadType, descriptor.ChannelAddress, workspaceVariable);
        statements.Append("JsonElement ").Append(outputsElementLocal).AppendLine(" = default;");

        OutputExtractionCode outputCode = OutputExtractionEmitter.Emit(
            stepId,
            outputs,
            workspaceVariable,
            "context",
            stepOutputLocals,
            inputsVariable,
            inputAccessors,
            responseBodyLocal: null,
            messagePayloadLocal: payloadLocal);

        fields.Append(outputCode.Fields);
        statements.Append(outputCode.Statements);

        return new ReceiveChannelStepCode(fields.ToString(), statements.ToString());
    }

    private static void AppendReceive(StringBuilder statements, string messageTransportVariable, string payloadType, string channelAddress, string workspaceVariable)
    {
        statements.Append(" = await ").Append(messageTransportVariable)
            .Append(".ReceiveOneAsync<").Append(payloadType).Append(">(").Append(EmitText.Quote(channelAddress))
            .Append("u8.ToArray(), ").Append(workspaceVariable).AppendLine(", cancellationToken).ConfigureAwait(false);");
    }
}

/// <summary>
/// The code emitted for a receive channel step (plan §5c).
/// </summary>
/// <param name="Fields">The <c>static readonly</c> field declarations to place on the executor class.</param>
/// <param name="Statements">The in-method statements that receive the message and build the step's outputs.</param>
internal readonly record struct ReceiveChannelStepCode(string Fields, string Statements);