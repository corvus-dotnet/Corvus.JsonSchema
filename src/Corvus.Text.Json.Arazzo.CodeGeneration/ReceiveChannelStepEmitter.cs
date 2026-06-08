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
/// <para>
/// When the step declares <c>outputs</c> they are projected from the received payload via
/// <see cref="OutputExtractionEmitter"/>: a <c>$message.payload[#/ptr]</c> expression navigates the
/// received message and copies only the addressed value into the step's outputs object. When the step
/// declares no outputs the whole received message becomes the step's outputs.
/// </para>
/// <para>
/// A step's <c>successCriteria</c> are inlined against the received payload (the same inliners the HTTP
/// path uses, with the payload as the live JSON body); a criterion referencing anything other than
/// <c>$message.*</c>/<c>$inputs</c>/<c>$steps</c> is rejected (a channel step has no request/response).
/// <c>$message.header.*</c>, correlationId matching, timeouts, and parameterised addresses are later phases.
/// </para>
/// </remarks>
internal static class ReceiveChannelStepEmitter
{
    private static readonly string[] ForbiddenCriterionTokens = ["$statusCode", "$response", "$request", "$method", "$url"];

    /// <summary>
    /// Emits the statements that receive one message, gate it on any success criteria, and expose it as
    /// the step's outputs. Fields and JSONPath auxiliary types are written into the supplied builders.
    /// </summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="channel">The resolved channel operation.</param>
    /// <param name="messageTransportVariable">The in-scope <c>IMessageTransport</c> variable name.</param>
    /// <param name="outputs">The step's declared outputs (name → runtime expression); empty for whole-payload capture.</param>
    /// <param name="successCriteria">The step's success criteria (may reference only <c>$message.*</c>/<c>$inputs</c>/<c>$steps</c>).</param>
    /// <param name="workspaceVariable">The in-scope <c>JsonWorkspace</c> variable name.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputsVariable">The workflow inputs variable name (for <c>$inputs</c> navigation).</param>
    /// <param name="inputAccessors">The input accessor map, or <see langword="null"/> for untyped inputs.</param>
    /// <param name="fields">Accumulates <c>static readonly</c> field declarations (e.g. baked literals / compiled criteria).</param>
    /// <param name="auxiliaryTypes">Accumulates sibling types (e.g. ahead-of-time JSONPath classes) emitted after the executor class.</param>
    /// <param name="namespaceName">The executor's namespace (for generated JSONPath sibling types).</param>
    /// <returns>The emitted in-method statements (the step-outputs element local is declared and assigned).</returns>
    public static string Emit(
        string stepId,
        in ResolvedChannel channel,
        string messageTransportVariable,
        IReadOnlyList<OutputMapping> outputs,
        IReadOnlyList<StepCriterion> successCriteria,
        string workspaceVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors,
        StringBuilder fields,
        StringBuilder auxiliaryTypes,
        string namespaceName)
    {
        AsyncApiChannelDescriptor descriptor = channel.Channel;

        if (descriptor.ChannelParameters.Count > 0)
        {
            throw new NotSupportedException($"Channel step '{stepId}' receives on a parameterised channel '{descriptor.ChannelAddress}'; parameterised channel addresses are a later phase.");
        }

        ValidateCriteria(stepId, successCriteria);

        // The message payload type drives the typed receive; an untyped payload schema falls back to JsonElement.
        string payloadType = descriptor.Messages.Count > 0 && descriptor.Messages[0].PayloadTypeName is { } typeName
            ? typeName
            : "Corvus.Text.Json.JsonElement";

        string identifier = EmitText.SanitizeIdentifier(stepId);
        string camel = EmitText.ToCamelCase(identifier);
        string outputsElementLocal = EmitText.StepOutputsElementLocal(stepId);
        string payloadLocal = $"{camel}MessagePayload";
        string successLocal = $"{camel}Success";
        bool hasOutputs = outputs.Count > 0;
        bool hasCriteria = successCriteria.Count > 0;

        // The received payload is materialised in the subscriber handler — while it is still live — so we
        // copy only what the step actually uses (the declared outputs, or the whole message when none are
        // declared) into the run's workspace, never cloning the whole message just to extract one field.
        var statements = new StringBuilder();
        var lambdaBody = new StringBuilder();
        statements.AppendLine("ArazzoTelemetry.StepsExecuted.Add(1);");
        statements.Append("JsonElement ").Append(outputsElementLocal).AppendLine(" = default;");
        if (hasCriteria)
        {
            statements.Append("bool ").Append(successLocal).AppendLine(" = true;");
        }

        // The live message is bound to a JsonElement once; criteria and output projection read from it.
        lambdaBody.Append("JsonElement ").Append(payloadLocal).AppendLine(" = JsonElement.From(message);");

        if (hasCriteria)
        {
            string gateExpression = StepBodyEmitter.EmitCriteriaExpression(
                successCriteria,
                fields,
                lambdaBody,
                auxiliaryTypes,
                $"{identifier}Recv",
                "context",
                responseVar: string.Empty,
                responseBodyLocal: payloadLocal,
                inputsVariable,
                stepOutputLocals,
                inputAccessors,
                responseHeaders: null,
                requestContext: default,
                namespaceName);

            lambdaBody.Append(successLocal).Append(" = (").Append(gateExpression).AppendLine(");");
        }

        if (hasOutputs)
        {
            OutputExtractionCode outputCode = OutputExtractionEmitter.Emit(
                stepId, outputs, workspaceVariable, "context", stepOutputLocals, inputsVariable, inputAccessors, responseBodyLocal: null, messagePayloadLocal: payloadLocal, messageHeadersLocal: "messageHeaders");
            fields.Append(outputCode.Fields);
            lambdaBody.Append(outputCode.Statements);
        }
        else
        {
            // No declared outputs: the whole received message becomes the step's outputs (cloned out of
            // the live payload before the handler returns).
            lambdaBody.Append(outputsElementLocal).Append(" = ").Append(payloadLocal)
                .Append(".CloneAsBuilder(").Append(workspaceVariable).AppendLine(").RootElement;");
        }

        lambdaBody.AppendLine("return default;");

        statements.Append("await ").Append(messageTransportVariable).Append(".ReceiveOneAsync<").Append(payloadType)
            .Append(">(").Append(EmitText.Quote(descriptor.ChannelAddress)).AppendLine("u8.ToArray(), (message, messageHeaders) =>");
        statements.AppendLine("{");
        statements.Append(lambdaBody);
        statements.AppendLine("}, cancellationToken).ConfigureAwait(false);");

        if (hasCriteria)
        {
            statements.Append("if (!").Append(successLocal).AppendLine(")");
            statements.AppendLine("{");
            statements.Append("    throw new WorkflowStepFailedException(").Append(EmitText.Quote(stepId)).Append(", ")
                .Append(EmitText.Quote($"Step '{stepId}' did not satisfy its success criteria.")).AppendLine(");");
            statements.AppendLine("}");
        }

        return statements.ToString();
    }

    private static void ValidateCriteria(string stepId, IReadOnlyList<StepCriterion> criteria)
    {
        foreach (StepCriterion criterion in criteria)
        {
            string text = criterion.Context is { } context ? $"{criterion.Condition} {context}" : criterion.Condition;
            foreach (string token in ForbiddenCriterionTokens)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                {
                    throw new NotSupportedException(
                        $"Receive channel step '{stepId}' has a criterion referencing '{token}'; a channel step's criteria may reference only $message, $inputs, and $steps.");
                }
            }
        }
    }
}