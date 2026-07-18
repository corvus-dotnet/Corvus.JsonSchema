// <copyright file="SendChannelStepEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits a <c>send</c> AsyncAPI channel step (plan §5): the step publishes a message on a channel by
/// calling the generated producer's publish method through an <c>IMessageTransport</c>. The message
/// payload is the step's <c>requestBody</c> (a runtime expression), re-wrapped to the producer's
/// <c>{PayloadType}</c> with <c>From</c> (a free re-interpretation of the same backing JSON) so it converts
/// to the producer's <c>{PayloadType}.Source</c>.
/// </summary>
/// <remarks>
/// The generated producer owns the AsyncAPI protocol (channel templating, validation, bindings), so the
/// step calls it rather than the transport directly — mirroring how operation steps call the generated
/// OpenAPI client. When the send operation declares a <c>reply</c> (request/reply), the step instead
/// calls the producer's <c>SendAndReceive…Async</c> method and treats the returned reply as the step's
/// outputs (so a step's <c>$message.payload</c> outputs/criteria resolve against the reply); with a
/// <c>successFlagLocal</c> the reply also gates the step's success in the control-flow loop,
/// so a request/reply send carries <c>onSuccess</c>/<c>onFailure</c> actions like any other step.
/// Multi-message operations and message headers on a send step are later phases.
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
    /// <param name="cancellationTokenExpression">The in-scope cancellation-token expression.</param>
    /// <param name="captureCorrelation">Whether to register a correlation token published in the payload.</param>
    /// <param name="successFlagLocal">
    /// When set (control-flow mode), the request/reply reply gates this local instead of throwing on a
    /// failed criterion, and the outputs local (hoisted by the caller) is assigned only on success — so
    /// the step's onSuccess/onFailure actions dispatch on the reply. When <see langword="null"/>
    /// (straight-line), a failed criterion throws and the outputs local is declared here.
    /// </param>
    /// <returns>The emitted in-method statements.</returns>
    public static string Emit(
        string stepId,
        in ResolvedChannel channel,
        StepBody? requestBody,
        IReadOnlyList<OutputMapping> outputs,
        IReadOnlyList<StepCriterion> successCriteria,
        IReadOnlyList<StepArgument> arguments,
        string messageTransportVariable,
        string workspaceVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors,
        StringBuilder fields,
        StringBuilder auxiliaryTypes,
        string namespaceName,
        string cancellationTokenExpression = "cancellationToken",
        bool captureCorrelation = false,
        string? successFlagLocal = null)
    {
        AsyncApiChannelDescriptor descriptor = channel.Channel;

        if (descriptor.ProducerClassName is not { } producerClass)
        {
            throw new NotSupportedException($"Channel step '{stepId}' targets a non-send channel '{descriptor.ChannelAddress}'; only send channel steps are supported so far.");
        }

        if (descriptor.Messages.Count == 0)
        {
            throw new NotSupportedException($"Channel step '{stepId}' targets a channel '{descriptor.ChannelAddress}' with no message.");
        }

        // A single-message operation binds its one message directly; a multi-message operation selects the
        // message by payload-schema validity at send time (below). `selected` is the single-message binding
        // (and the first message, used for the header-argument shape that a single-message send shares).
        bool multiMessage = descriptor.Messages.Count > 1;
        AsyncApiChannelMessageDescriptor selected = descriptor.Messages[0];

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

        // Resolve the payload to a JsonElement. A runtime expression resolves to a reference; everything
        // else (a composite template, an interpolated string, or a literal) is built/baked and assigned —
        // matching the kinds an operation step's request body supports.
        if (body.Replacements is { Count: > 0 } replacements)
        {
            // Payload replacements overlay values (referencing $inputs/$steps) onto the base payload at
            // JSON Pointer targets — the same build-and-patch the operation request body uses.
            var replacementCleanup = new StringBuilder();
            string built = RequestBindingEmitter.EmitBodyWithReplacements(
                fields, statements, replacementCleanup, body, replacements, "context", stepOutputLocals, inputsVariable, inputAccessors, $"{identifier}_Payload");
            statements.Append("JsonElement ").Append(payloadLocal).Append(" = ").Append(built).AppendLine(";");
            statements.Append(replacementCleanup);
        }
        else if (body.Kind == ArgumentValueKind.Expression)
        {
            ValueResolution.Emit(fields, statements, body.Value, payloadLocal, "context", stepOutputLocals, $"{identifier}_Payload", inputsVariable, inputAccessors);
        }
        else
        {
            string built = body.Kind switch
            {
                ArgumentValueKind.CompositeTemplate => JsonTemplateEmitter.EmitComposite(
                    stepId, body.Value, workspaceVariable, default, inputsVariable, stepOutputLocals, inputAccessors, fields, statements, $"{identifier}_Payload"),
                ArgumentValueKind.Interpolation => JsonTemplateEmitter.EmitInterpolation(
                    stepId, body.Value, workspaceVariable, default, inputsVariable, stepOutputLocals, inputAccessors, statements, $"{identifier}_Payload"),
                ArgumentValueKind.LiteralComposite or ArgumentValueKind.LiteralNumber or ArgumentValueKind.LiteralBoolean =>
                    JsonTemplateEmitter.EmitConstant(body.Value, fields, $"{identifier}_PayloadConst"),
                ArgumentValueKind.LiteralNull => JsonTemplateEmitter.EmitConstant("null", fields, $"{identifier}_PayloadConst"),
                ArgumentValueKind.LiteralString => JsonTemplateEmitter.EmitConstant(System.Text.Json.JsonSerializer.Serialize(body.Value), fields, $"{identifier}_PayloadConst"),
                _ => throw new NotSupportedException($"Channel step '{stepId}' binds an unsupported {body.Kind} payload."),
            };
            statements.Append("JsonElement ").Append(payloadLocal).Append(" = ").Append(built).AppendLine(";");
        }

        statements.AppendLine("ArazzoTelemetry.StepsExecuted.Add(1);");
        statements.Append("var ").Append(producerVariable).Append(" = new ").Append(producerClass).Append('(').Append(messageTransportVariable).AppendLine(");");

        // Partition the step's parameters: `in: header` parameters populate the message headers; the rest
        // supply the channel-address placeholders.
        var addressArgs = new List<StepArgument>(arguments.Count);
        var headerArgs = new List<StepArgument>();
        foreach (StepArgument argument in arguments)
        {
            (string.Equals(argument.In, "header", StringComparison.Ordinal) ? headerArgs : addressArgs).Add(argument);
        }

        // Message headers on a multi-message send (where the selected message — and thus its headers type —
        // is only known at run time) are a later phase; combine them one at a time for now.
        if (multiMessage)
        {
            bool anyHeaders = headerArgs.Count > 0;
            foreach (AsyncApiChannelMessageDescriptor message in descriptor.Messages)
            {
                anyHeaders |= message.HeadersTypeName is not null;
            }

            if (anyHeaders)
            {
                throw new NotSupportedException(
                    $"Channel step '{stepId}' targets a multi-message channel '{descriptor.ChannelAddress}' with message headers; multi-message send with headers is a later phase.");
            }
        }

        // A parameterised channel: the generated producer method takes a string argument per channel
        // parameter (in declaration order), resolved here from the step's non-header parameters.
        var channelArgs = new StringBuilder();
        foreach ((string _, string local) in ChannelAddressEmitter.ResolveParameters(
            descriptor.ChannelParameters, addressArgs, fields, statements, $"{identifier}_Ch", stepOutputLocals, inputsVariable, inputAccessors))
        {
            channelArgs.Append(", ").Append(local);
        }

        // Message headers: assembled from the `in: header` parameters into the message's generated headers
        // type and passed to the producer after the payload. The producer method requires the argument
        // whenever the message declares a headers schema (HeadersTypeName), so it is emitted for every
        // header-declaring message — an empty object when no header parameters are supplied. Setting
        // headers on a message that declares none is an error.
        string headersArgument = string.Empty;
        string? headersElementLocal = null;
        if (selected.HeadersTypeName is { } headersType)
        {
            string headersExpr = EmitHeadersObject(headerArgs, identifier, camel, workspaceVariable, stepOutputLocals, inputsVariable, inputAccessors, fields, statements);

            // A stable local so both the producer argument and a header-located correlation capture read the
            // same assembled headers element.
            headersElementLocal = $"{camel}HeadersElement";
            statements.Append("JsonElement ").Append(headersElementLocal).Append(" = ").Append(headersExpr).AppendLine(";");
            headersArgument = ", " + RequestBindingEmitter.ConvertToSourceType(headersElementLocal, headersType);
        }
        else if (headerArgs.Count > 0)
        {
            throw new NotSupportedException(
                $"Channel step '{stepId}' sets message headers (parameters with in: header), but the message '{selected.MessageName}' on channel '{descriptor.ChannelAddress}' declares no headers schema.");
        }

        if (!isRequestReply)
        {
            if (multiMessage)
            {
                EmitMultiMessagePublish(descriptor, stepId, payloadLocal, camel, producerVariable, channelArgs.ToString(), captureCorrelation, cancellationTokenExpression, statements);
                return statements.ToString();
            }

            if (selected.ProducerMethodName is not { } publishMethod)
            {
                throw new NotSupportedException($"Channel step '{stepId}' targets a channel '{descriptor.ChannelAddress}' with no publishable message.");
            }

            // Re-wrap the JsonElement payload to the message's model type with From so the single
            // model → {Type}.Source implicit conversion applies at the producer call (C# will not chain
            // JsonElement → model → Source).
            string publishPayload = RequestBindingEmitter.ConvertToSourceType(payloadLocal, selected.PayloadTypeName ?? "Corvus.Text.Json.JsonElement");
            statements.Append("await ").Append(producerVariable).Append('.').Append(publishMethod).Append('(').Append(publishPayload).Append(headersArgument).Append(channelArgs).Append(", ").Append(cancellationTokenExpression).AppendLine(").ConfigureAwait(false);");

            EmitCorrelationCapture(captureCorrelation, selected, camel, payloadLocal, headersElementLocal, statements);
            return statements.ToString();
        }

        if (multiMessage)
        {
            throw new NotSupportedException(
                $"Channel step '{stepId}' targets a multi-message request/reply channel '{descriptor.ChannelAddress}'; multi-message request/reply selection is a later phase.");
        }

        // ── request/reply: send and capture the reply as the step's outputs ──
        if (selected.RequestReplyMethodName is not { } requestMethod)
        {
            throw new NotSupportedException($"Channel step '{stepId}' targets a request/reply channel '{descriptor.ChannelAddress}' with no request/reply method.");
        }

        ReceiveChannelStepEmitter.ValidateCriteria(stepId, successCriteria);

        string replyType = descriptor.ReplyPayloadTypeName!;
        string replyLocal = $"{camel}Reply";
        string replyPayloadLocal = $"{camel}ReplyPayload";
        string outputsElementLocal = EmitText.StepOutputsElementLocal(stepId);

        statements.Append(replyType).Append(' ').Append(replyLocal).Append(" = await ").Append(producerVariable).Append('.')
            .Append(requestMethod).Append('(').Append(payloadLocal).Append(headersArgument).Append(channelArgs).Append(", ").Append(cancellationTokenExpression).AppendLine(").ConfigureAwait(false);");
        statements.Append("JsonElement ").Append(replyPayloadLocal).Append(" = JsonElement.From(").Append(replyLocal).AppendLine(");");

        if (successFlagLocal is { } successFlag)
        {
            // ── control-flow request/reply: the reply gates the step's success flag; outputs (hoisted by
            // the caller) project only on success, so onSuccess/onFailure dispatch on the reply ──
            var gateOps = new StringBuilder();
            string gateExpression = successCriteria.Count == 0
                ? "true"
                : StepBodyEmitter.EmitCriteriaExpression(
                    successCriteria, fields, gateOps, auxiliaryTypes, $"{identifier}Reply", "context",
                    responseVar: string.Empty, new CriterionSources(replyPayloadLocal), inputsVariable, stepOutputLocals, inputAccessors, null, default, namespaceName);

            if (ReceiveChannelStepEmitter.UsesContext(gateOps, gateExpression))
            {
                statements.Append("context.SetMessagePayload(").Append(replyPayloadLocal).AppendLine(");");
            }

            statements.Append(gateOps);
            statements.Append(successFlag).Append(" = (").Append(gateExpression).AppendLine(");");
            statements.Append("if (").Append(successFlag).AppendLine(")");
            statements.AppendLine("{");

            var outputsBuilder = new StringBuilder();
            if (outputs.Count > 0)
            {
                OutputExtractionCode outputCode = OutputExtractionEmitter.Emit(
                    stepId, outputs, workspaceVariable, "context", stepOutputLocals, inputsVariable, inputAccessors, responseBodyLocal: null, messagePayloadLocal: replyPayloadLocal);
                fields.Append(outputCode.Fields);
                outputsBuilder.Append(outputCode.Statements);
            }
            else
            {
                // No declared outputs: the whole reply becomes the step's outputs.
                outputsBuilder.Append(outputsElementLocal).Append(" = ").Append(replyPayloadLocal)
                    .Append(".CloneAsBuilder(").Append(workspaceVariable).AppendLine(").RootElement;");
            }

            WorkflowExecutorEmitter.AppendIndented(statements, outputsBuilder.ToString(), 4);
            statements.AppendLine("}");
            return statements.ToString();
        }

        // ── straight-line request/reply: a failed criterion throws, and the outputs local is declared here ──
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

    /// <summary>
    /// Assembles the message headers object from a send step's <c>in: header</c> parameters: each header
    /// value is resolved (any kind) to a <see cref="Corvus.Text.Json.JsonElement"/> and added under its
    /// parameter name — the same param-to-object build a sub-workflow step uses for its inputs. Returns the
    /// expression for the assembled headers element (an empty object when there are no header parameters).
    /// </summary>
    private static string EmitHeadersObject(
        IReadOnlyList<StepArgument> headerArgs,
        string identifier,
        string camel,
        string workspaceVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors,
        StringBuilder fields,
        StringBuilder statements)
    {
        if (headerArgs.Count == 0)
        {
            // A header-declaring message whose step sets no headers still needs the required argument;
            // an empty object satisfies the signature (and any required headers fail the producer's
            // pre-send validation, which is the correct feedback).
            return JsonTemplateEmitter.EmitConstant("{}", fields, $"{identifier}_HeadersEmpty");
        }

        var valueLocals = new List<string>(headerArgs.Count);
        foreach (StepArgument headerArg in headerArgs)
        {
            string local = $"{camel}Header{valueLocals.Count.ToString(CultureInfo.InvariantCulture)}";
            string field = $"{identifier}_Header_{EmitText.SanitizeIdentifier(headerArg.Name)}";
            RequestBindingEmitter.EmitValueAsElement(
                fields, statements, headerArg.Kind, headerArg.Value, "context", stepOutputLocals, inputsVariable, inputAccessors, field, local);
            valueLocals.Add(local);
        }

        string builderVariable = $"{camel}Headers";
        statements.Append("Span<JsonElement> ").Append(builderVariable).Append("Values = [")
            .Append(string.Join(", ", valueLocals)).AppendLine("];");
        statements.Append("var ").Append(builderVariable).Append(" = JsonElement.CreateBuilder(").Append(workspaceVariable).AppendLine(",");
        statements.Append("    (ReadOnlySpan<JsonElement>)").Append(builderVariable).AppendLine("Values,");
        statements.AppendLine("    static (in ReadOnlySpan<JsonElement> values, ref JsonElement.ObjectBuilder builder) =>");
        statements.AppendLine("    {");
        for (int i = 0; i < headerArgs.Count; i++)
        {
            // Omit a header whose value did not resolve (a default/Undefined JsonElement); a None-valued
            // property would trip the builder's assert. A resolved null is kept.
            string index = i.ToString(CultureInfo.InvariantCulture);
            statements.Append("        if (values[").Append(index).AppendLine("].IsNotUndefined())");
            statements.AppendLine("        {");
            statements.Append("            builder.AddProperty(").Append(EmitText.Quote(headerArgs[i].Name)).Append("u8, values[").Append(index).AppendLine("]);");
            statements.AppendLine("        }");
        }

        statements.AppendLine("    });");
        return $"{builderVariable}.RootElement";
    }

    // Emits a multi-message fire-and-forget send: the payload is validated against each message's schema in
    // order (the same zero-allocation EvaluateSchema check the receive-side MatchMessage uses) and dispatched
    // to the first (per AsyncAPI, the only) matching message's producer method. A payload matching no message
    // is a runtime step failure. Message headers are not combined with multi-message selection yet (rejected
    // up front), so no headers argument is emitted here.
    private static void EmitMultiMessagePublish(
        in AsyncApiChannelDescriptor descriptor,
        string stepId,
        string payloadLocal,
        string camel,
        string producerVariable,
        string channelArgs,
        bool captureCorrelation,
        string cancellationTokenExpression,
        StringBuilder statements)
    {
        for (int i = 0; i < descriptor.Messages.Count; i++)
        {
            AsyncApiChannelMessageDescriptor message = descriptor.Messages[i];
            if (message.PayloadTypeName is not { } payloadType || message.ProducerMethodName is not { } publishMethod)
            {
                throw new NotSupportedException(
                    $"Channel step '{stepId}' targets a multi-message channel '{descriptor.ChannelAddress}' whose message '{message.MessageName}' has no distinct payload type to select on.");
            }

            string candidate = $"{camel}Candidate{i.ToString(CultureInfo.InvariantCulture)}";
            statements.Append(payloadType).Append(' ').Append(candidate).Append(" = ").Append(payloadType).Append(".From(").Append(payloadLocal).AppendLine(");");
            statements.Append("if (").Append(candidate).AppendLine(".EvaluateSchema())");
            statements.AppendLine("{");
            statements.Append("    await ").Append(producerVariable).Append('.').Append(publishMethod).Append('(').Append(candidate).Append(channelArgs).Append(", ").Append(cancellationTokenExpression).AppendLine(").ConfigureAwait(false);");

            var capture = new StringBuilder();
            EmitCorrelationCapture(captureCorrelation, message, camel, payloadLocal, null, capture);
            WorkflowExecutorEmitter.AppendIndented(statements, capture.ToString(), 4);

            statements.AppendLine("}");
            statements.AppendLine("else");
            statements.AppendLine("{");
        }

        statements.Append("    throw new WorkflowStepFailedException(").Append(EmitText.Quote(stepId)).Append(", ")
            .Append(EmitText.Quote($"Step '{stepId}' payload matched none of the channel's messages.")).AppendLine(");");

        for (int i = 0; i < descriptor.Messages.Count; i++)
        {
            statements.AppendLine("}");
        }
    }

    // When the workflow correlates (some receive step declares a correlationId) and this send's message
    // declares an AsyncAPI Correlation ID, read the token from the published payload — or, for a
    // header-located id, from the headers the send set (its in:header parameters) — and register it under
    // the correlation id name so a later correlated receive can match it. A header-located id with no headers
    // set on the send is skipped (nothing to capture).
    private static void EmitCorrelationCapture(
        bool captureCorrelation,
        in AsyncApiChannelMessageDescriptor message,
        string camel,
        string payloadLocal,
        string? headersElementLocal,
        StringBuilder statements)
    {
        if (!captureCorrelation || message.CorrelationIdName is not { } correlationName || message.CorrelationIdLocation is not { } location)
        {
            return;
        }

        AsyncApiRuntimeExpression locationExpression = AsyncApiRuntimeExpression.Parse(location);
        if (locationExpression.JsonPointer is not { } pointer)
        {
            return;
        }

        string source;
        if (locationExpression.Kind == AsyncApiRuntimeExpressionKind.MessageHeader)
        {
            if (headersElementLocal is null)
            {
                // The correlation id is header-located but this send set no headers; nothing to register.
                return;
            }

            source = headersElementLocal;
        }
        else if (locationExpression.Kind == AsyncApiRuntimeExpressionKind.MessagePayload)
        {
            source = payloadLocal;
        }
        else
        {
            return;
        }

        string tokenLocal = $"{camel}CorrelationToken";
        statements.Append("if (CorrelationToken.TryRead(").Append(source).Append(", ").Append(EmitText.Quote(pointer)).Append("u8, out byte[] ").Append(tokenLocal).AppendLine("))");
        statements.AppendLine("{");
        statements.Append("    correlationTokens[").Append(EmitText.Quote(correlationName)).Append("] = ").Append(tokenLocal).AppendLine(";");
        statements.AppendLine("}");
    }
}