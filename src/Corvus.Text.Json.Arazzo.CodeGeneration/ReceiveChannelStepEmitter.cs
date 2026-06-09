// <copyright file="ReceiveChannelStepEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo;
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
    /// <param name="requestBody">The step's request body — for a request/reply <c>receive</c> (responder), the reply payload to send back; <see langword="null"/> for fire-and-forget receive.</param>
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
        StepBody? requestBody,
        IReadOnlyList<StepArgument> arguments,
        string workspaceVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors,
        StringBuilder fields,
        StringBuilder auxiliaryTypes,
        string namespaceName,
        string? correlationName = null,
        string? correlationLocation = null)
    {
        AsyncApiChannelDescriptor descriptor = channel.Channel;

        ValidateCriteria(stepId, successCriteria);

        // The message payload type drives the typed receive; an untyped payload schema falls back to JsonElement.
        string payloadType = descriptor.Messages.Count > 0 && descriptor.Messages[0].PayloadTypeName is { } typeName
            ? typeName
            : "Corvus.Text.Json.JsonElement";

        // A receive operation that declares a reply is a request/reply responder: the step receives one
        // request, replies with its requestBody (correlated by the transport), and unsubscribes (one-shot).
        bool isResponder = descriptor.ReplyPayloadTypeName is not null;

        if (correlationName is not null && isResponder)
        {
            throw new NotSupportedException(
                $"Receive channel step '{stepId}' declares a correlationId on a request/reply (responder) step; request/reply correlation is handled by the transport, so a step-level correlationId applies only to plain receive steps.");
        }

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
            var gateOps = new StringBuilder();
            string gateExpression = StepBodyEmitter.EmitCriteriaExpression(
                successCriteria,
                fields,
                gateOps,
                auxiliaryTypes,
                $"{identifier}Recv",
                "context",
                responseVar: string.Empty,
                sources: new CriterionSources(payloadLocal, "messageHeaders"),
                inputsVariable,
                stepOutputLocals,
                inputAccessors,
                responseHeaders: null,
                requestContext: default,
                namespaceName);

            // A dynamic-pattern criterion that could not be inlined evaluates a CompiledCriterion against
            // the context; feed the received message into it (the context's inputs are set at method scope).
            if (UsesContext(gateOps, gateExpression))
            {
                lambdaBody.Append("context.SetMessagePayload(").Append(payloadLocal).AppendLine(");");
            }

            lambdaBody.Append(gateOps);
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

        // The subscription address — a constant for a static channel, or built at runtime from the step's
        // parameters for a parameterised channel.
        string address = ChannelAddressEmitter.EmitReceiveAddress(
            descriptor, arguments, fields, statements, $"{identifier}_Ch", stepOutputLocals, inputsVariable, inputAccessors);

        if (isResponder)
        {
            // Resolve the reply payload (the step's requestBody) against the live request, $inputs, and
            // prior step outputs, and return it; the transport serializes it synchronously while the
            // request and the workspace are still live, so the reply is a reference (never copied).
            string replyType = descriptor.ReplyPayloadTypeName!;
            string replyExpression = EmitReplyResolution(
                stepId, requestBody, replyType, payloadLocal, workspaceVariable, $"{identifier}Reply", inputsVariable, stepOutputLocals, inputAccessors, fields, lambdaBody);
            lambdaBody.Append("return new ValueTask<").Append(replyType).Append(">(").Append(replyExpression).AppendLine(");");

            statements.Append("await ").Append(messageTransportVariable).Append(".ReceiveOneAndReplyAsync<")
                .Append(payloadType).Append(", ").Append(replyType).Append(">(")
                .Append(address).AppendLine(", (message, messageHeaders) =>");
            statements.AppendLine("{");
            statements.Append(lambdaBody);
            statements.AppendLine("}, cancellationToken).ConfigureAwait(false);");
        }
        else if (correlationName is not null)
        {
            lambdaBody.AppendLine("return default;");

            // Correlated receive: only the message carrying the token a prior send registered under this
            // name completes the step. With no registered token the workflow never published the request,
            // so the step fails rather than waiting for a message that can never correlate.
            string expectedLocal = $"{camel}Expected";
            statements.Append("if (!correlationTokens.TryGetValue(").Append(EmitText.Quote(correlationName)).Append(", out byte[]? ").Append(expectedLocal).AppendLine("))");
            statements.AppendLine("{");
            statements.Append("    throw new WorkflowStepFailedException(").Append(EmitText.Quote(stepId)).Append(", ")
                .Append(EmitText.Quote($"Step '{stepId}' has correlationId '{correlationName}' but no prior step registered a correlation token to match.")).AppendLine(");");
            statements.AppendLine("}");

            statements.Append("await ").Append(messageTransportVariable).Append(".ReceiveOneAsync<").Append(payloadType)
                .Append(">(").Append(address).AppendLine(", (message, messageHeaders) =>");
            statements.AppendLine("{");
            statements.Append(lambdaBody);
            statements.Append("}, cancellationToken, (message, messageHeaders) => CorrelationToken.Matches(JsonElement.From(message), ")
                .Append(EmitText.Quote(correlationLocation!)).Append("u8, ").Append(expectedLocal).AppendLine(")).ConfigureAwait(false);");
        }
        else
        {
            lambdaBody.AppendLine("return default;");

            statements.Append("await ").Append(messageTransportVariable).Append(".ReceiveOneAsync<").Append(payloadType)
                .Append(">(").Append(address).AppendLine(", (message, messageHeaders) =>");
            statements.AppendLine("{");
            statements.Append(lambdaBody);
            statements.AppendLine("}, cancellationToken).ConfigureAwait(false);");
        }

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

    /// <summary>
    /// Emits the statements that resolve a request/reply <c>receive</c> step's reply payload (its
    /// <c>requestBody</c>) and returns the expression that yields the typed reply.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A runtime-expression reply (e.g. <c>$message.payload</c> to echo the request, a pointer projection
    /// of it, an <c>$inputs</c> value, or a prior step's output) is resolved by static navigation — a
    /// reference into the live request / inputs / step outputs, never copied. A constant reply (a literal
    /// scalar or a literal object/array with no embedded expressions) is parsed once into a standalone
    /// document and referenced. Both reference forms outlive the handler because the transport serializes
    /// the reply synchronously while the request, the workspace, and these constants are all live.
    /// </para>
    /// <para>
    /// Interpolated replies and composite replies that embed runtime expressions (building a reply object
    /// from request fields) are a later phase — they need the template-substitution engine the request
    /// side does not yet share for the message-payload source.
    /// </para>
    /// </remarks>
    internal static string EmitReplyResolution(
        string stepId,
        StepBody? requestBody,
        string replyType,
        string payloadLocal,
        string workspaceVariable,
        string baseName,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        StringBuilder fields,
        StringBuilder statements)
    {
        if (requestBody is not { } body)
        {
            throw new NotSupportedException($"Request/reply receive step '{stepId}' has no requestBody; a responder step must declare the reply payload to send back.");
        }

        var sources = new CriterionSources(payloadLocal, "messageHeaders");

        // Resolve the base reply payload to a JsonElement expression (message/inputs/step-aware).
        string baseExpression = EmitReplyValueElement(stepId, body.Value, body.Kind, sources, workspaceVariable, baseName, inputsVariable, stepOutputLocals, inputAccessors, fields, statements);

        // No replacements: the base is the reply.
        if (body.Replacements is not { Count: > 0 } replacements)
        {
            return $"({replyType})({baseExpression})";
        }

        // Replacements overlay values onto the base reply at JSON Pointer targets — the same build-and-patch
        // the request side uses, but with message-aware value resolution.
        string builderLocal = $"{baseName}Builder";
        string mutableLocal = $"{baseName}Mutable";
        statements.Append("var ").Append(builderLocal).Append(" = ").Append(baseExpression).Append('.').Append("CreateBuilder(").Append(workspaceVariable).AppendLine(");");
        statements.Append("JsonElement.Mutable ").Append(mutableLocal).Append(" = ").Append(builderLocal).AppendLine(".RootElement;");

        for (int i = 0; i < replacements.Count; i++)
        {
            PayloadReplacement replacement = replacements[i];
            string valueExpression = EmitReplyValueElement(
                stepId, replacement.Value, replacement.Kind, sources, workspaceVariable, $"{baseName}Repl{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}", inputsVariable, stepOutputLocals, inputAccessors, fields, statements);
            statements.Append(mutableLocal).Append(".TryAdd(").Append(EmitText.Quote(replacement.Target)).Append("u8, ").Append(valueExpression).AppendLine(");");
        }

        string patchedLocal = $"{baseName}Patched";
        statements.Append("JsonElement ").Append(patchedLocal).Append(" = ").Append(mutableLocal).AppendLine(";");
        return $"({replyType})({patchedLocal})";
    }

    /// <summary>
    /// Resolves a responder reply value (the base payload or a replacement value) of any kind to a
    /// <see cref="JsonElement"/> expression, message/inputs/step-aware. A leading-<c>$</c> string that
    /// matches no known runtime-expression form is treated as a literal (Arazzo defines no <c>$</c> escape).
    /// </summary>
    private static string EmitReplyValueElement(
        string stepId,
        string value,
        ArgumentValueKind kind,
        in CriterionSources sources,
        string workspaceVariable,
        string baseName,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        StringBuilder fields,
        StringBuilder statements)
    {
        switch (kind)
        {
            case ArgumentValueKind.Expression:
            {
                (ArazzoExpression expression, string? navigationPointer) = CriterionExpressionParsing.SplitNavigation(value);
                if (expression.Source == ArazzoExpressionSource.Literal)
                {
                    return JsonTemplateEmitter.EmitConstant(System.Text.Json.JsonSerializer.Serialize(value), fields, $"{baseName}Constant");
                }

                if (!CriterionExpressionParsing.TryEmitElementNavigation(
                        expression, navigationPointer, baseName, sources, inputsVariable, stepOutputLocals, inputAccessors, statements, out string elementLocal))
                {
                    throw new NotSupportedException($"Request/reply receive step '{stepId}' has a reply value '{value}' that cannot be resolved; a responder reply may reference only $message, $inputs, and $steps.");
                }

                return elementLocal;
            }

            case ArgumentValueKind.Interpolation:
                return JsonTemplateEmitter.EmitInterpolation(
                    stepId, value, workspaceVariable, sources, inputsVariable, stepOutputLocals, inputAccessors, statements, baseName);

            case ArgumentValueKind.CompositeTemplate:
                return JsonTemplateEmitter.EmitComposite(
                    stepId, value, workspaceVariable, sources, inputsVariable, stepOutputLocals, inputAccessors, fields, statements, baseName);

            default:
            {
                string constantJson = kind switch
                {
                    ArgumentValueKind.LiteralComposite or ArgumentValueKind.LiteralNumber or ArgumentValueKind.LiteralBoolean => value,
                    ArgumentValueKind.LiteralNull => "null",
                    ArgumentValueKind.LiteralString => System.Text.Json.JsonSerializer.Serialize(value),
                    _ => throw new NotSupportedException($"Request/reply receive step '{stepId}' binds an unsupported reply payload kind '{kind}'."),
                };

                return JsonTemplateEmitter.EmitConstant(constantJson, fields, $"{baseName}Constant");
            }
        }
    }

    // True when the emitted gate still resolves through the WorkflowExecutionContext (a criterion that
    // could not be inlined — e.g. a dynamic regex/jsonpath pattern — fell back to a CompiledCriterion).
    internal static bool UsesContext(StringBuilder gateOperands, string gateExpression)
        => gateExpression.Contains("context", StringComparison.Ordinal)
            || gateOperands.ToString().Contains("context", StringComparison.Ordinal);

    internal static void ValidateCriteria(string stepId, IReadOnlyList<StepCriterion> criteria)
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