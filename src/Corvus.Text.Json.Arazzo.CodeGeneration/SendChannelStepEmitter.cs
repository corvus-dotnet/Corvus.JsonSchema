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
/// OpenAPI client. When the send operation declares a <c>reply</c> (request/reply), the step instead
/// calls the producer's <c>SendAndReceive…Async</c> method and treats the returned reply as the step's
/// outputs (so a step's <c>$message.payload</c> outputs/criteria resolve against the reply). Parameterised
/// channel addresses, multi-message operations, headers, and non-expression payloads are later phases.
/// </remarks>
internal static class SendChannelStepEmitter
{
    /// <summary>
    /// Emits the statements that publish a message (fire-and-forget) or send a request and capture its
    /// reply (request/reply). Fields and JSONPath auxiliary types are written into the supplied builders.
    /// </summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="channel">The resolved channel operation.</param>
    /// <param name="requestBody">The step's request body (the message payload), or <see langword="null"/>.</param>
    /// <param name="outputs">The step's declared outputs (request/reply only; projected from the reply).</param>
    /// <param name="successCriteria">The step's success criteria (request/reply only; gate the reply).</param>
    /// <param name="messageTransportVariable">The in-scope <c>IMessageTransport</c> variable name.</param>
    /// <param name="workspaceVariable">The in-scope <c>JsonWorkspace</c> variable name.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputsVariable">The workflow inputs variable name (for <c>$inputs</c> navigation).</param>
    /// <param name="inputAccessors">The input accessor map, or <see langword="null"/> for untyped inputs.</param>
    /// <param name="fields">Accumulates <c>static readonly</c> field declarations.</param>
    /// <param name="auxiliaryTypes">Accumulates sibling types (e.g. ahead-of-time JSONPath classes).</param>
    /// <param name="namespaceName">The executor's namespace (for generated JSONPath sibling types).</param>
    /// <returns>The emitted in-method statements.</returns>
    public static string Emit(
        string stepId,
        in ResolvedChannel channel,
        StepBody? requestBody,
        IReadOnlyList<OutputMapping> outputs,
        IReadOnlyList<StepCriterion> successCriteria,
        string messageTransportVariable,
        string workspaceVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors,
        StringBuilder fields,
        StringBuilder auxiliaryTypes,
        string namespaceName)
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

        if (descriptor.Messages.Count == 0)
        {
            throw new NotSupportedException($"Channel step '{stepId}' targets a channel '{descriptor.ChannelAddress}' with no message.");
        }

        if (requestBody is not { } body)
        {
            throw new NotSupportedException($"Channel step '{stepId}' has no requestBody payload to publish.");
        }

        bool isRequestReply = descriptor.ReplyPayloadTypeName is not null;
        string identifier = EmitText.SanitizeIdentifier(stepId);
        string camel = EmitText.ToCamelCase(identifier);
        string payloadLocal = $"{camel}Payload";
        string producerVariable = $"{camel}Producer";

        var statements = new StringBuilder();

        // Resolve the payload to a JsonElement: a runtime expression resolves to a reference; a composite
        // template (object/array embedding $inputs/$steps) is built into the run workspace.
        switch (body.Kind)
        {
            case ArgumentValueKind.Expression:
                ValueResolution.Emit(fields, statements, body.Value, payloadLocal, "context", stepOutputLocals, $"{identifier}_Payload", inputsVariable, inputAccessors);
                break;

            case ArgumentValueKind.CompositeTemplate:
                string built = JsonTemplateEmitter.EmitComposite(
                    stepId, body.Value, workspaceVariable, default, inputsVariable, stepOutputLocals, inputAccessors, fields, statements, $"{identifier}_Payload");
                statements.Append("JsonElement ").Append(payloadLocal).Append(" = ").Append(built).AppendLine(";");
                break;

            default:
                throw new NotSupportedException($"Channel step '{stepId}' binds a {body.Kind} payload; only runtime-expression and composite-template payloads are supported on a send channel step.");
        }

        statements.AppendLine("ArazzoTelemetry.StepsExecuted.Add(1);");
        statements.Append("var ").Append(producerVariable).Append(" = new ").Append(producerClass).Append('(').Append(messageTransportVariable).AppendLine(");");

        if (!isRequestReply)
        {
            if (descriptor.Messages[0].ProducerMethodName is not { } publishMethod)
            {
                throw new NotSupportedException($"Channel step '{stepId}' targets a channel '{descriptor.ChannelAddress}' with no publishable message.");
            }

            statements.Append("await ").Append(producerVariable).Append('.').Append(publishMethod).Append('(').Append(payloadLocal).AppendLine(", cancellationToken).ConfigureAwait(false);");
            return statements.ToString();
        }

        // ── request/reply: send and capture the reply as the step's outputs ──
        if (descriptor.Messages[0].RequestReplyMethodName is not { } requestMethod)
        {
            throw new NotSupportedException($"Channel step '{stepId}' targets a request/reply channel '{descriptor.ChannelAddress}' with no request/reply method.");
        }

        ReceiveChannelStepEmitter.ValidateCriteria(stepId, successCriteria);

        string replyType = descriptor.ReplyPayloadTypeName!;
        string replyLocal = $"{camel}Reply";
        string replyPayloadLocal = $"{camel}ReplyPayload";
        string outputsElementLocal = EmitText.StepOutputsElementLocal(stepId);

        statements.Append(replyType).Append(' ').Append(replyLocal).Append(" = await ").Append(producerVariable).Append('.')
            .Append(requestMethod).Append('(').Append(payloadLocal).AppendLine(", cancellationToken).ConfigureAwait(false);");
        statements.Append("JsonElement ").Append(replyPayloadLocal).Append(" = JsonElement.From(").Append(replyLocal).AppendLine(");");

        if (successCriteria.Count > 0)
        {
            var gateOps = new StringBuilder();
            string gateExpression = StepBodyEmitter.EmitCriteriaExpression(
                successCriteria, fields, gateOps, auxiliaryTypes, $"{identifier}Reply", "context",
                responseVar: string.Empty, new CriterionSources(replyPayloadLocal), inputsVariable, stepOutputLocals, inputAccessors, null, default, namespaceName);

            if (ReceiveChannelStepEmitter.UsesContext(gateOps, gateExpression))
            {
                statements.Append("context.SetMessagePayload(").Append(replyPayloadLocal).AppendLine(");");
            }

            statements.Append(gateOps);
            statements.Append("if (!(").Append(gateExpression).AppendLine("))");
            statements.AppendLine("{");
            statements.Append("    throw new WorkflowStepFailedException(").Append(EmitText.Quote(stepId)).Append(", ")
                .Append(EmitText.Quote($"Step '{stepId}' did not satisfy its success criteria.")).AppendLine(");");
            statements.AppendLine("}");
        }

        if (outputs.Count > 0)
        {
            statements.Append("JsonElement ").Append(outputsElementLocal).AppendLine(" = default;");
            OutputExtractionCode outputCode = OutputExtractionEmitter.Emit(
                stepId, outputs, workspaceVariable, "context", stepOutputLocals, inputsVariable, inputAccessors, responseBodyLocal: null, messagePayloadLocal: replyPayloadLocal);
            fields.Append(outputCode.Fields);
            statements.Append(outputCode.Statements);
        }
        else
        {
            // No declared outputs: the whole reply becomes the step's outputs.
            statements.Append("JsonElement ").Append(outputsElementLocal).Append(" = ").Append(replyPayloadLocal)
                .Append(".CloneAsBuilder(").Append(workspaceVariable).AppendLine(").RootElement;");
        }

        return statements.ToString();
    }
}