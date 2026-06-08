// <copyright file="ControlFlowEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits a workflow executor body as a labelled loop (a <c>while(true)</c> over a <c>switch(__state)</c>)
/// when the workflow uses control flow — any step declares an <c>onSuccess</c>/<c>onFailure</c> action
/// (plan §3.3). Each step's success gate becomes a boolean; the first matching action then sets the next
/// state (<c>goto</c>), schedules a retry (<c>retry</c>, with a hoisted per-step counter and an optional
/// delay), or jumps to building the outputs (<c>end</c>). With no matching action the default applies —
/// next step on success, fail the workflow on failure.
/// </summary>
/// <remarks>
/// Action criteria are inlined exactly like success criteria (via
/// <see cref="StepBodyEmitter.EmitCriteriaExpression"/>), so a control-flow step stays context-free when
/// every criterion is statically resolvable. Action criteria are evaluated <em>inside</em> the step's
/// <c>try</c> (while the response is alive); the chosen effect is captured in case-local flags and applied
/// after the response is disposed. Sub-workflow <c>goto</c> (a <c>workflowId</c> target) and reusable
/// <c>$ref</c> actions are later phases.
/// </remarks>
internal static class ControlFlowEmitter
{
    public static void Emit(
        IReadOnlyList<ControlFlowStep> steps,
        in ArazzoDocument.WorkflowObject workflow,
        in WorkflowExecutorOptions options,
        StringBuilder fields,
        StringBuilder body,
        StringBuilder auxiliaryTypes,
        Dictionary<string, string> stepOutputLocals)
    {
        // A goto targets a step by id; build the id→state-index map and pre-register every step's
        // outputs local so $steps.<id>.outputs references resolve regardless of execution order.
        var stepIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < steps.Count; i++)
        {
            stepIndex[steps[i].StepId] = i;
            if (ProducesOutputs(steps[i]))
            {
                stepOutputLocals[steps[i].StepId] = EmitText.StepOutputsElementLocal(steps[i].StepId);
            }
        }

        var loop = new StringBuilder();

        // Hoist the per-step state that must survive across loop iterations: each step's outputs object
        // (read by later steps) and each retried step's attempt counter.
        foreach (ControlFlowStep step in steps)
        {
            if (ProducesOutputs(step))
            {
                loop.Append("JsonElement ").Append(EmitText.StepOutputsElementLocal(step.StepId)).AppendLine(" = default;");
            }

            if (NeedsRetryCounter(step))
            {
                loop.Append("int ").Append(RetryCounter(step.StepId)).AppendLine(" = 0;");
            }
        }

        loop.AppendLine("int __state = 0;");
        loop.AppendLine("while (true)");
        loop.AppendLine("{");
        loop.AppendLine("    switch (__state)");
        loop.AppendLine("    {");

        for (int i = 0; i < steps.Count; i++)
        {
            loop.Append("        case ").Append(i.ToString(CultureInfo.InvariantCulture)).AppendLine(":");
            if (steps[i].SubWorkflowId is not null)
            {
                EmitSubWorkflowCase(steps[i], i, stepIndex, options, fields, loop, auxiliaryTypes, stepOutputLocals);
            }
            else if (steps[i].Channel is not null)
            {
                EmitChannelCase(steps[i], i, stepIndex, options, fields, loop, auxiliaryTypes, stepOutputLocals);
            }
            else
            {
                EmitCase(steps[i], i, stepIndex, options, fields, loop, auxiliaryTypes, stepOutputLocals);
            }
        }

        // A state past the last step (the last step's default "next") completes the workflow.
        loop.AppendLine("        default: goto __workflowOutputs;");
        loop.AppendLine("    }");
        loop.AppendLine("}");
        loop.AppendLine();
        loop.AppendLine("__workflowOutputs:");

        WorkflowExecutorEmitter.AppendIndented(body, loop.ToString(), 12);

        // The outputs are built after the loop label, at the method-body indent (like the straight-line path).
        WorkflowExecutorEmitter.AppendWorkflowOutputs(fields, body, workflow, stepOutputLocals, options.InputAccessors);
    }

    private static void EmitCase(
        in ControlFlowStep step,
        int index,
        IReadOnlyDictionary<string, int> stepIndex,
        in WorkflowExecutorOptions options,
        StringBuilder fields,
        StringBuilder loop,
        StringBuilder auxiliaryTypes,
        Dictionary<string, string> stepOutputLocals)
    {
        ResolvedOperation operation = step.Operation!.Value;
        string identifier = EmitText.SanitizeIdentifier(step.StepId);
        string prefix = $"{identifier}_";
        string camel = EmitText.ToCamelCase(identifier);
        string clientVar = $"{camel}Client";
        string responseVar = $"{camel}Response";
        string responseBodyLocal = $"{camel}ResponseBody";
        string? bindBodyLocal = step.BindResponseBody ? responseBodyLocal : null;

        var requestContext = new StepRequestContext(
            operation.Operation.Method.ToString().ToUpperInvariant(), step.Arguments, step.RequestBody);

        RequestBindingCode request = RequestBindingEmitter.Emit(
            operation, step.Arguments, "context", prefix, stepOutputLocals, "inputs", options.InputAccessors, step.RequestBody);
        fields.Append(request.Fields);

        // Build the success gate + action dispatch first, so we can tell whether anything still resolves
        // through the WorkflowExecutionContext (a non-inlined criterion). Only then is the context fed.
        var gate = new StringBuilder();
        string successExpression = step.SuccessCriteria.Count == 0
            ? $"{responseVar}.IsSuccess"
            : StepBodyEmitter.EmitCriteriaExpression(
                step.SuccessCriteria, fields, gate, auxiliaryTypes, prefix, "context", responseVar, new CriterionSources(bindBodyLocal),
                "inputs", stepOutputLocals, options.InputAccessors, operation.Operation.ResponseHeaders, requestContext, options.Namespace);

        gate.Append("bool ").Append(camel).Append("Success = (").Append(successExpression).AppendLine(");");
        gate.Append("if (").Append(camel).AppendLine("Success)");
        gate.AppendLine("{");

        if (step.Outputs.Count > 0)
        {
            OutputExtractionCode outputCode = OutputExtractionEmitter.Emit(
                step.StepId, step.Outputs, "workspace", "context", stepOutputLocals, "inputs", options.InputAccessors, bindBodyLocal);
            fields.Append(outputCode.Fields);
            WorkflowExecutorEmitter.AppendIndented(gate, outputCode.Statements, 4);
        }

        EmitDispatch(gate, step.OnSuccess, isFailure: false, camel, prefix, responseVar, new CriterionSources(bindBodyLocal), stepIndex, fields, auxiliaryTypes, operation.Operation.ResponseHeaders, requestContext, stepOutputLocals, options);
        gate.AppendLine("}");
        gate.AppendLine("else");
        gate.AppendLine("{");
        EmitDispatch(gate, step.OnFailure, isFailure: true, camel, prefix, responseVar, new CriterionSources(bindBodyLocal), stepIndex, fields, auxiliaryTypes, operation.Operation.ResponseHeaders, requestContext, stepOutputLocals, options);
        gate.AppendLine("}");

        string gateText = gate.ToString();
        bool usesContext = gateText.Contains("context", StringComparison.Ordinal);

        // ---- assemble the case block ----
        var c = new StringBuilder();
        c.AppendLine("{");
        c.Append("    // ── step: ").Append(step.StepId).AppendLine(" ──");
        WorkflowExecutorEmitter.AppendIndented(c, request.Statements, 4);
        c.Append("    var ").Append(clientVar).Append(" = new ").Append(operation.Operation.ClientTypeName).Append('(').AppendLine("transport);");

        var callArguments = new List<string>(request.NamedArguments) { "cancellationToken: cancellationToken" };
        c.Append("    var ").Append(responseVar).Append(" = await ").Append(clientVar).Append('.')
            .Append(operation.Operation.ClientMethodName).Append('(').Append(string.Join(", ", callArguments)).AppendLine(").ConfigureAwait(false);");
        WorkflowExecutorEmitter.AppendIndented(c, request.Cleanup, 4);

        c.Append("    int ").Append(camel).Append("Next = ").Append((index + 1).ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        c.Append("    bool ").Append(camel).AppendLine("End = false;");
        c.Append("    bool ").Append(camel).AppendLine("Fail = false;");
        c.Append("    bool ").Append(camel).AppendLine("Retry = false;");
        c.Append("    double ").Append(camel).AppendLine("RetryDelay = 0;");
        c.AppendLine("    try");
        c.AppendLine("    {");
        c.AppendLine("        ArazzoTelemetry.StepsExecuted.Add(1);");
        if (usesContext)
        {
            c.Append("        context.SetResponseStatusCode(").Append(responseVar).AppendLine(".StatusCode);");
        }

        if (step.BindResponseBody)
        {
            c.Append("        JsonElement ").Append(responseBodyLocal).AppendLine(" = default;");
            foreach (ResponseDescriptor response in operation.Operation.Responses)
            {
                if (response.BodyPropertyName is { } bodyProperty
                    && int.TryParse(response.StatusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out int statusCode))
                {
                    c.Append("        if (").Append(responseVar).Append(".StatusCode == ").Append(statusCode.ToString(CultureInfo.InvariantCulture))
                        .Append(") { ").Append(responseBodyLocal).Append(" = (JsonElement)").Append(responseVar).Append('.').Append(bodyProperty).AppendLine("; }");
                }
            }

            if (usesContext)
            {
                c.Append("        context.SetResponseBody(").Append(responseBodyLocal).AppendLine(");");
            }
        }

        WorkflowExecutorEmitter.AppendIndented(c, gateText, 8);
        c.AppendLine("    }");
        c.AppendLine("    finally");
        c.AppendLine("    {");
        c.Append("        await ").Append(responseVar).AppendLine(".DisposeAsync().ConfigureAwait(false);");
        c.AppendLine("    }");

        // Apply the captured control-flow decision now the response is disposed.
        c.Append("    if (").Append(camel).Append("Fail) { throw new WorkflowStepFailedException(")
            .Append(EmitText.Quote(step.StepId)).Append(", ").Append(EmitText.Quote($"Step '{step.StepId}' did not satisfy its success criteria.")).AppendLine("); }");
        c.Append("    if (").Append(camel).AppendLine("End) { goto __workflowOutputs; }");
        c.Append("    if (").Append(camel).AppendLine("Retry)");
        c.AppendLine("    {");
        c.Append("        if (").Append(camel).Append("RetryDelay > 0) { await Task.Delay(TimeSpan.FromSeconds(").Append(camel)
            .AppendLine("RetryDelay), cancellationToken).ConfigureAwait(false); }");
        c.Append("        __state = ").Append(index.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        c.AppendLine("        continue;");
        c.AppendLine("    }");
        c.Append("    __state = ").Append(camel).AppendLine("Next;");
        c.AppendLine("    continue;");
        c.AppendLine("}");

        WorkflowExecutorEmitter.AppendIndented(loop, c.ToString(), 12);
    }

    private static void EmitSubWorkflowCase(
        in ControlFlowStep step,
        int index,
        IReadOnlyDictionary<string, int> stepIndex,
        in WorkflowExecutorOptions options,
        StringBuilder fields,
        StringBuilder loop,
        StringBuilder auxiliaryTypes,
        Dictionary<string, string> stepOutputLocals)
    {
        string subWorkflowId = step.SubWorkflowId!;
        string identifier = EmitText.SanitizeIdentifier(step.StepId);
        string prefix = $"{identifier}_";
        string camel = EmitText.ToCamelCase(identifier);
        string outputsLocal = EmitText.StepOutputsElementLocal(step.StepId);
        string completedLocal = $"{camel}Completed";
        string successLocal = $"{camel}Success";

        // Success criteria (and action criteria) of a sub-workflow step have no HTTP response — they run
        // against the sub-workflow's outputs / the inputs, so no response var / headers / request context.
        var gate = new StringBuilder();
        string successExpression = step.SuccessCriteria.Count == 0
            ? "true"
            : StepBodyEmitter.EmitCriteriaExpression(
                step.SuccessCriteria, fields, gate, auxiliaryTypes, prefix, "context", string.Empty, default,
                "inputs", stepOutputLocals, options.InputAccessors, null, default, options.Namespace);

        var successDispatch = new StringBuilder();
        EmitDispatch(successDispatch, step.OnSuccess, isFailure: false, camel, prefix, string.Empty, default, stepIndex, fields, auxiliaryTypes, null, default, stepOutputLocals, options);
        var failureDispatch = new StringBuilder();
        EmitDispatch(failureDispatch, step.OnFailure, isFailure: true, camel, prefix, string.Empty, default, stepIndex, fields, auxiliaryTypes, null, default, stepOutputLocals, options);

        var inputs = new StringBuilder();
        string builderVariable = SubWorkflowStepEmitter.BuildInputs(fields, inputs, step.StepId, step.Arguments, stepOutputLocals, "inputs", options.InputAccessors);
        string targetClass = SubWorkflowStepEmitter.TargetClass(options.Namespace, subWorkflowId);

        var c = new StringBuilder();
        c.AppendLine("{");
        c.Append("    // ── step: ").Append(step.StepId).Append(" (workflow: ").Append(subWorkflowId).AppendLine(") ──");
        WorkflowExecutorEmitter.AppendIndented(c, inputs.ToString(), 4);
        c.Append("    int ").Append(camel).Append("Next = ").Append((index + 1).ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        c.Append("    bool ").Append(camel).AppendLine("End = false;");
        c.Append("    bool ").Append(camel).AppendLine("Fail = false;");
        c.Append("    bool ").Append(camel).AppendLine("Retry = false;");
        c.Append("    double ").Append(camel).AppendLine("RetryDelay = 0;");
        c.Append("    bool ").Append(completedLocal).AppendLine(";");

        // The sub-workflow fails by throwing WorkflowStepFailedException; catch it so onFailure can act.
        c.AppendLine("    try");
        c.AppendLine("    {");
        c.AppendLine("        ArazzoTelemetry.StepsExecuted.Add(1);");
        c.Append("        ").Append(outputsLocal).Append(" = await ").Append(targetClass)
            .Append(".ExecuteAsync(transport, workspace, ").Append(builderVariable).AppendLine(".RootElement, cancellationToken).ConfigureAwait(false);");
        c.Append("        ").Append(completedLocal).AppendLine(" = true;");
        c.AppendLine("    }");
        c.AppendLine("    catch (WorkflowStepFailedException)");
        c.AppendLine("    {");
        c.Append("        ").Append(completedLocal).AppendLine(" = false;");
        c.AppendLine("    }");

        c.Append("    bool ").Append(successLocal).AppendLine(";");
        c.Append("    if (").Append(completedLocal).AppendLine(")");
        c.AppendLine("    {");
        WorkflowExecutorEmitter.AppendIndented(c, gate.ToString(), 8);
        c.Append("        ").Append(successLocal).Append(" = (").Append(successExpression).AppendLine(");");
        c.AppendLine("    }");
        c.AppendLine("    else");
        c.AppendLine("    {");
        c.Append("        ").Append(successLocal).AppendLine(" = false;");
        c.AppendLine("    }");

        c.Append("    if (").Append(successLocal).AppendLine(")");
        c.AppendLine("    {");
        WorkflowExecutorEmitter.AppendIndented(c, successDispatch.ToString(), 8);
        c.AppendLine("    }");
        c.AppendLine("    else");
        c.AppendLine("    {");
        WorkflowExecutorEmitter.AppendIndented(c, failureDispatch.ToString(), 8);
        c.AppendLine("    }");

        c.Append("    if (").Append(camel).Append("Fail) { throw new WorkflowStepFailedException(")
            .Append(EmitText.Quote(step.StepId)).Append(", ").Append(EmitText.Quote($"Step '{step.StepId}' did not satisfy its success criteria.")).AppendLine("); }");
        c.Append("    if (").Append(camel).AppendLine("End) { goto __workflowOutputs; }");
        c.Append("    if (").Append(camel).AppendLine("Retry)");
        c.AppendLine("    {");
        c.Append("        if (").Append(camel).Append("RetryDelay > 0) { await Task.Delay(TimeSpan.FromSeconds(").Append(camel)
            .AppendLine("RetryDelay), cancellationToken).ConfigureAwait(false); }");
        c.Append("        __state = ").Append(index.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        c.AppendLine("        continue;");
        c.AppendLine("    }");
        c.Append("    __state = ").Append(camel).AppendLine("Next;");
        c.AppendLine("    continue;");
        c.AppendLine("}");

        WorkflowExecutorEmitter.AppendIndented(loop, c.ToString(), 12);
    }

    private static void EmitChannelCase(
        in ControlFlowStep step,
        int index,
        IReadOnlyDictionary<string, int> stepIndex,
        in WorkflowExecutorOptions options,
        StringBuilder fields,
        StringBuilder loop,
        StringBuilder auxiliaryTypes,
        Dictionary<string, string> stepOutputLocals)
    {
        ResolvedChannel channel = step.Channel!.Value;
        AsyncApiChannelDescriptor descriptor = channel.Channel;
        bool isReceive = descriptor.Action == OperationAction.Receive;
        string identifier = EmitText.SanitizeIdentifier(step.StepId);
        string prefix = $"{identifier}_";
        string camel = EmitText.ToCamelCase(identifier);

        // Action criteria are dispatched after the message handler returns (so a goto/return is valid and
        // the payload is no longer live), so they may reference only $inputs/$steps — never $message.*.
        ValidateChannelActionCriteria(step.StepId, step.OnSuccess);
        ValidateChannelActionCriteria(step.StepId, step.OnFailure);

        // Dispatch runs after the step; for a channel step it has no response/request/message context.
        var successDispatch = new StringBuilder();
        EmitDispatch(successDispatch, step.OnSuccess, isFailure: false, camel, prefix, string.Empty, default, stepIndex, fields, auxiliaryTypes, null, default, stepOutputLocals, options);
        var failureDispatch = new StringBuilder();
        EmitDispatch(failureDispatch, step.OnFailure, isFailure: true, camel, prefix, string.Empty, default, stepIndex, fields, auxiliaryTypes, null, default, stepOutputLocals, options);

        var c = new StringBuilder();
        c.AppendLine("{");
        c.Append("    // ── step: ").Append(step.StepId).AppendLine(" (channel) ──");
        c.Append("    int ").Append(camel).Append("Next = ").Append((index + 1).ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        c.Append("    bool ").Append(camel).AppendLine("End = false;");
        c.Append("    bool ").Append(camel).AppendLine("Fail = false;");
        c.Append("    bool ").Append(camel).AppendLine("Retry = false;");
        c.Append("    double ").Append(camel).AppendLine("RetryDelay = 0;");
        c.Append("    bool ").Append(camel).AppendLine("Success = false;");

        if (isReceive)
        {
            EmitReceiveBody(c, step, descriptor, camel, prefix, fields, auxiliaryTypes, stepOutputLocals, options);
        }
        else
        {
            // Send: publish the payload, then the step succeeds unconditionally.
            SendChannelStepCode send = SendChannelStepEmitter.Emit(
                step.StepId, channel, step.RequestBody, "messageTransport", stepOutputLocals, "inputs", options.InputAccessors);
            fields.Append(send.Fields);
            WorkflowExecutorEmitter.AppendIndented(c, send.Statements, 4);
            c.Append("    ").Append(camel).AppendLine("Success = true;");
        }

        c.Append("    if (").Append(camel).AppendLine("Success)");
        c.AppendLine("    {");
        WorkflowExecutorEmitter.AppendIndented(c, successDispatch.ToString(), 8);
        c.AppendLine("    }");
        c.AppendLine("    else");
        c.AppendLine("    {");
        WorkflowExecutorEmitter.AppendIndented(c, failureDispatch.ToString(), 8);
        c.AppendLine("    }");

        c.Append("    if (").Append(camel).Append("Fail) { throw new WorkflowStepFailedException(")
            .Append(EmitText.Quote(step.StepId)).Append(", ").Append(EmitText.Quote($"Step '{step.StepId}' did not satisfy its success criteria.")).AppendLine("); }");
        c.Append("    if (").Append(camel).AppendLine("End) { goto __workflowOutputs; }");
        c.Append("    if (").Append(camel).AppendLine("Retry)");
        c.AppendLine("    {");
        c.Append("        if (").Append(camel).Append("RetryDelay > 0) { await Task.Delay(TimeSpan.FromSeconds(").Append(camel)
            .AppendLine("RetryDelay), cancellationToken).ConfigureAwait(false); }");
        c.Append("        __state = ").Append(index.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        c.AppendLine("        continue;");
        c.AppendLine("    }");
        c.Append("    __state = ").Append(camel).AppendLine("Next;");
        c.AppendLine("    continue;");
        c.AppendLine("}");

        WorkflowExecutorEmitter.AppendIndented(loop, c.ToString(), 12);
    }

    // Emits the receive into the case body: the message is handled inline (while the payload is live) to
    // evaluate the success criteria and project outputs; the success flag and outputs element escape to
    // the surrounding case for action dispatch.
    private static void EmitReceiveBody(
        StringBuilder c,
        in ControlFlowStep step,
        in AsyncApiChannelDescriptor descriptor,
        string camel,
        string prefix,
        StringBuilder fields,
        StringBuilder auxiliaryTypes,
        Dictionary<string, string> stepOutputLocals,
        in WorkflowExecutorOptions options)
    {
        if (descriptor.ChannelParameters.Count > 0)
        {
            throw new NotSupportedException($"Channel step '{step.StepId}' receives on a parameterised channel '{descriptor.ChannelAddress}'; parameterised channel addresses are a later phase.");
        }

        ReceiveChannelStepEmitter.ValidateCriteria(step.StepId, step.SuccessCriteria);

        string payloadType = descriptor.Messages.Count > 0 && descriptor.Messages[0].PayloadTypeName is { } typeName
            ? typeName
            : "Corvus.Text.Json.JsonElement";
        string payloadLocal = $"{camel}MessagePayload";
        string outputsLocal = EmitText.StepOutputsElementLocal(step.StepId);

        // The message handler reads the live payload: evaluate the success gate, then project outputs.
        var lambdaBody = new StringBuilder();
        lambdaBody.Append("JsonElement ").Append(payloadLocal).AppendLine(" = JsonElement.From(message);");

        var sources = new CriterionSources(payloadLocal, "messageHeaders");
        var gateOps = new StringBuilder();
        string gateExpression = step.SuccessCriteria.Count == 0
            ? "true"
            : StepBodyEmitter.EmitCriteriaExpression(
                step.SuccessCriteria, fields, gateOps, auxiliaryTypes, $"{prefix}recv", "context",
                responseVar: string.Empty, sources, "inputs", stepOutputLocals, options.InputAccessors, null, default, options.Namespace);

        // A dynamic-pattern criterion that could not be inlined evaluates a CompiledCriterion against the
        // context; feed the received message into it (the context's inputs are set at method scope).
        if (ReceiveChannelStepEmitter.UsesContext(gateOps, gateExpression))
        {
            lambdaBody.Append("context.SetMessagePayload(").Append(payloadLocal).AppendLine(");");
        }

        lambdaBody.Append(gateOps);
        lambdaBody.Append(camel).Append("Success = (").Append(gateExpression).AppendLine(");");
        lambdaBody.Append("if (").Append(camel).AppendLine("Success)");
        lambdaBody.AppendLine("{");
        if (step.Outputs.Count > 0)
        {
            OutputExtractionCode outputCode = OutputExtractionEmitter.Emit(
                step.StepId, step.Outputs, "workspace", "context", stepOutputLocals, "inputs", options.InputAccessors, responseBodyLocal: null, messagePayloadLocal: payloadLocal, messageHeadersLocal: "messageHeaders");
            fields.Append(outputCode.Fields);
            WorkflowExecutorEmitter.AppendIndented(lambdaBody, outputCode.Statements, 4);
        }
        else
        {
            lambdaBody.Append("    ").Append(outputsLocal).Append(" = ").Append(payloadLocal).AppendLine(".CloneAsBuilder(workspace).RootElement;");
        }

        lambdaBody.AppendLine("}");
        lambdaBody.AppendLine("return default;");

        c.AppendLine("    ArazzoTelemetry.StepsExecuted.Add(1);");
        c.Append("    await messageTransport.ReceiveOneAsync<").Append(payloadType).Append(">(")
            .Append(EmitText.Quote(descriptor.ChannelAddress)).AppendLine("u8.ToArray(), (message, messageHeaders) =>");
        c.AppendLine("    {");
        WorkflowExecutorEmitter.AppendIndented(c, lambdaBody.ToString(), 8);
        c.AppendLine("    }, cancellationToken).ConfigureAwait(false);");
    }

    private static void ValidateChannelActionCriteria(string stepId, IReadOnlyList<StepActionInfo> actions)
    {
        ReadOnlySpan<string> forbidden = ["$message", "$statusCode", "$response", "$request", "$method", "$url"];
        foreach (StepActionInfo action in actions)
        {
            foreach (StepCriterion criterion in action.Criteria)
            {
                string text = criterion.Context is { } context ? $"{criterion.Condition} {context}" : criterion.Condition;
                foreach (string token in forbidden)
                {
                    if (text.Contains(token, StringComparison.Ordinal))
                    {
                        throw new NotSupportedException(
                            $"Channel step '{stepId}' has an onSuccess/onFailure action criterion referencing '{token}'; a channel step's action criteria run after the message is handled and may reference only $inputs and $steps.");
                    }
                }
            }
        }
    }

    private static bool ProducesOutputs(in ControlFlowStep step)
        => step.SubWorkflowId is not null
            || step.Outputs.Count > 0
            || (step.Channel is { } channel && channel.Channel.Action == OperationAction.Receive);

    private static void EmitDispatch(
        StringBuilder target,
        IReadOnlyList<StepActionInfo> actions,
        bool isFailure,
        string camel,
        string prefix,
        string responseVar,
        CriterionSources sources,
        IReadOnlyDictionary<string, int> stepIndex,
        StringBuilder fields,
        StringBuilder auxiliaryTypes,
        IReadOnlyList<ResponseHeaderInfo>? responseHeaders,
        StepRequestContext requestContext,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        in WorkflowExecutorOptions options)
    {
        // Resolve each action's criteria expression (its operand statements are appended to `target`
        // before the if-chain) and its effect. First-match wins, so once an unconditional action (empty
        // criteria) is reached the remaining actions are unreachable and dropped.
        var resolved = new List<(string Expression, string Apply)>();
        int actionNumber = 0;
        foreach (StepActionInfo action in actions)
        {
            if (action.Kind == StepActionKind.Retry && !isFailure)
            {
                // retry is a failure action only; ignore it on the success path.
                continue;
            }

            string actionPrefix = $"{prefix}{(isFailure ? "F" : "S")}{actionNumber.ToString(CultureInfo.InvariantCulture)}_";
            actionNumber++;
            string expression = StepBodyEmitter.EmitCriteriaExpression(
                action.Criteria, fields, target, auxiliaryTypes, actionPrefix, "context", responseVar, sources,
                "inputs", stepOutputLocals, options.InputAccessors, responseHeaders, requestContext, options.Namespace);

            resolved.Add((expression, BuildApply(action, camel, stepIndex, options.Namespace)));
            if (action.Criteria.Count == 0)
            {
                break;
            }
        }

        bool first = true;
        bool hasUnconditional = false;
        foreach ((string expression, string apply) in resolved)
        {
            if (expression == "true")
            {
                if (first)
                {
                    target.Append(apply);
                }
                else
                {
                    target.AppendLine("else");
                    target.AppendLine("{");
                    target.Append("    ").Append(apply);
                    target.AppendLine("}");
                }

                hasUnconditional = true;
                break;
            }

            target.Append(first ? "if (" : "else if (").Append(expression).AppendLine(")");
            target.AppendLine("{");
            target.Append("    ").Append(apply);
            target.AppendLine("}");
            first = false;
        }

        if (!hasUnconditional && isFailure)
        {
            // No failure action matched: fail the workflow (the straight-line default).
            string fail = $"{camel}Fail = true;\n";
            if (first)
            {
                target.Append(fail);
            }
            else
            {
                target.AppendLine("else");
                target.AppendLine("{");
                target.Append("    ").Append(fail);
                target.AppendLine("}");
            }
        }

        // Success with no matching action falls through to the default next state (already set).
    }

    private static string BuildApply(in StepActionInfo action, string camel, IReadOnlyDictionary<string, int> stepIndex, string workflowsNamespace)
    {
        switch (action.Kind)
        {
            case StepActionKind.End:
                return $"{camel}End = true;\n";

            case StepActionKind.Goto:
                if (action.TargetWorkflowId is { } targetWorkflow)
                {
                    // goto a workflow = transfer control: run the target workflow and return its result
                    // as this workflow's result. The current inputs flow through (a JsonElement converts
                    // to the target's inputs type via its implicit operator).
                    return $"return await {SubWorkflowStepEmitter.TargetClass(workflowsNamespace, targetWorkflow)}.ExecuteAsync(transport, workspace, (JsonElement)inputs, cancellationToken).ConfigureAwait(false);\n";
                }

                if (action.TargetStepId is not { } targetStep || !stepIndex.TryGetValue(targetStep, out int target))
                {
                    throw new InvalidOperationException(
                        $"Action '{action.Name}' performs a goto to unknown step '{action.TargetStepId}'.");
                }

                return $"{camel}Next = {target.ToString(CultureInfo.InvariantCulture)};\n";

            case StepActionKind.Retry:
            {
                int limit = action.RetryLimit ?? 1;
                string delay = (action.RetryAfter ?? 0).ToString(CultureInfo.InvariantCulture);
                return $"if ({RetryCounterFromCamel(camel)} < {limit.ToString(CultureInfo.InvariantCulture)}) {{ {RetryCounterFromCamel(camel)}++; {camel}Retry = true; {camel}RetryDelay = {delay}; }} else {{ {camel}Fail = true; }}\n";
            }

            default:
                return $"{camel}End = true;\n";
        }
    }

    private static bool NeedsRetryCounter(in ControlFlowStep step)
    {
        foreach (StepActionInfo action in step.OnFailure)
        {
            if (action.Kind == StepActionKind.Retry)
            {
                return true;
            }
        }

        return false;
    }

    private static string RetryCounter(string stepId) => $"{EmitText.ToCamelCase(EmitText.SanitizeIdentifier(stepId))}RetryCount";

    private static string RetryCounterFromCamel(string camel) => $"{camel}RetryCount";
}