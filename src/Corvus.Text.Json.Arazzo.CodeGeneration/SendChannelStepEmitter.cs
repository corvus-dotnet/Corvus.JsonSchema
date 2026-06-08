// <copyright file="SendChannelStepEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits a <c>send</c> AsyncAPI channel step (plan §5): the step publishes a message on a channel by
/// calling the generated producer's publish method through an <c>IMessageTransport</c>. The message
/// payload is the step's <c>requestBody</c> (a runtime expression), passed as the producer's
/// <c>{PayloadType}.Source</c> (a <see cref="JsonElement"/> converts via the implicit operator).
/// </summary>
/// <remarks>
/// The generated producer owns the AsyncAPI protocol (channel templating, validation, bindings), so the
/// step calls it rather than the transport directly — mirroring how operation steps call the generated
/// OpenAPI client. First increment: a static-address, single-message send whose payload is a runtime
/// expression; parameterised channel addresses, multi-message operations, headers, and non-expression
/// payloads are later phases.
/// </remarks>
internal static class SendChannelStepEmitter
{
    /// <summary>
    /// Emits the fields and statements that publish a message for a send channel step.
    /// </summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="channel">The resolved channel operation.</param>
    /// <param name="requestBody">The step's request body (the message payload), or <see langword="null"/>.</param>
    /// <param name="messageTransportVariable">The in-scope <c>IMessageTransport</c> variable name.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputsVariable">The workflow inputs variable name (for <c>$inputs</c> navigation).</param>
    /// <param name="inputAccessors">The input accessor map, or <see langword="null"/> for untyped inputs.</param>
    /// <returns>The emitted fields and statements.</returns>
    public static SendChannelStepCode Emit(
        string stepId,
        in ResolvedChannel channel,
        StepBody? requestBody,
        string messageTransportVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors)
    {
        AsyncApiChannelDescriptor descriptor = channel.Channel;

        if (descriptor.ProducerClassName is not { } producerClass)
        {
            throw new NotSupportedException($"Channel step '{stepId}' targets a non-send channel '{descriptor.ChannelAddress}'; only send channel steps are supported so far.");
        }

        if (descriptor.ChannelParameters.Count > 0)
        {
            throw new NotSupportedException($"Channel step '{stepId}' targets a parameterised channel '{descriptor.ChannelAddress}'; parameterised channel addresses are a later phase.");
        }

        if (descriptor.Messages.Count == 0 || descriptor.Messages[0].ProducerMethodName is not { } publishMethod)
        {
            throw new NotSupportedException($"Channel step '{stepId}' targets a channel '{descriptor.ChannelAddress}' with no publishable message.");
        }

        if (requestBody is not { } body)
        {
            throw new NotSupportedException($"Channel step '{stepId}' has no requestBody payload to publish.");
        }

        if (body.Kind != ArgumentValueKind.Expression)
        {
            throw new NotSupportedException($"Channel step '{stepId}' binds a non-expression payload; only runtime-expression payloads are supported on a send channel step.");
        }

        string identifier = EmitText.SanitizeIdentifier(stepId);
        string camel = EmitText.ToCamelCase(identifier);
        string payloadLocal = $"{camel}Payload";
        string producerVariable = $"{camel}Producer";

        var fields = new StringBuilder();
        var statements = new StringBuilder();

        // Resolve the payload expression to a JsonElement reference, then publish it via the producer.
        ValueResolution.Emit(fields, statements, body.Value, payloadLocal, "context", stepOutputLocals, $"{identifier}_Payload", inputsVariable, inputAccessors);

        statements.AppendLine("ArazzoTelemetry.StepsExecuted.Add(1);");
        statements.Append("var ").Append(producerVariable).Append(" = new ").Append(producerClass).Append('(').Append(messageTransportVariable).AppendLine(");");
        statements.Append("await ").Append(producerVariable).Append('.').Append(publishMethod).Append('(').Append(payloadLocal).AppendLine(", cancellationToken).ConfigureAwait(false);");

        return new SendChannelStepCode(fields.ToString(), statements.ToString());
    }
}

/// <summary>
/// The code emitted for a send channel step (plan §5).
/// </summary>
/// <param name="Fields">The <c>static readonly</c> field declarations to place on the executor class.</param>
/// <param name="Statements">The in-method statements that publish the message.</param>
internal readonly record struct SendChannelStepCode(string Fields, string Statements);