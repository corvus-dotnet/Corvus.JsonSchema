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
        Dictionary<string, string> stepOutputLocals,
        TransportSelection selection,
        bool usesCorrelation = false)
    {
        // A goto targets a step by id; build the id→state-index map and pre-register every step's
        // outputs local so $steps.<id>.outputs references resolve regardless of execution order.
        var stepIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < steps.Count; i++)
        {
            stepIndex[steps[i].StepId] = i;
            if (ProducesOutputs(steps[i]))
            {
                string outputsLocal = EmitText.StepOutputsElementLocal(steps[i].StepId);
                stepOutputLocals[steps[i].StepId] = outputsLocal;

                // Also key a sub-workflow invocation's outputs by sub-workflow id, and each bound argument's
                // value by sub-workflow id + parameter name, so $workflows.<id>.outputs.<name> /
                // $workflows.<id>.inputs.<name> navigate them statically (no context). The inputs keys point at
                // stable hoisted argument values (assigned in the step's case) rather than the transient
                // inputs builder.
                if (steps[i].SubWorkflowId is { } subWorkflowId)
                {
                    stepOutputLocals["$workflows:" + subWorkflowId] = outputsLocal;
                    foreach (StepArgument argument in steps[i].Arguments)
                    {
                        stepOutputLocals["$workflows-input:" + subWorkflowId + ":" + argument.Name] =
                            SubWorkflowInputVar(steps[i].StepId, argument.Name);
                    }
                }
            }
        }

        var loop = new StringBuilder();

        // Hoist the per-step state that must survive across loop iterations: each step's outputs object
        // (read by later steps) and each retried step's attempt counter. In durable mode each local restores
        // from the run (so a resumed run re-enters with its products already populated) instead of `default`.
        bool durable = options.Durable;
        foreach (ControlFlowStep step in steps)
        {
            if (ProducesOutputs(step))
            {
                string outputsLocal = EmitText.StepOutputsElementLocal(step.StepId);
                if (durable)
                {
                    loop.Append("JsonElement ").Append(outputsLocal).Append(" = run is not null && run.TryGetStepOutputs(")
                        .Append(EmitText.Quote(step.StepId)).Append(", out JsonElement ").Append(outputsLocal).Append("Restored) ? ")
                        .Append(outputsLocal).AppendLine("Restored : default;");
                }
                else
                {
                    loop.Append("JsonElement ").Append(outputsLocal).AppendLine(" = default;");
                }

                // A sub-workflow invocation also exposes the inputs it was passed ($workflows.<id>.inputs.<name>);
                // hoist each bound argument's value so later steps can navigate it. These are not restored on a
                // durable resume (inputs are not persisted), so after resuming past this step they are default —
                // matching the pre-existing behaviour where the inputs qualifier was unavailable.
                if (step.SubWorkflowId is not null)
                {
                    foreach (StepArgument argument in step.Arguments)
                    {
                        loop.Append("JsonElement ").Append(SubWorkflowInputVar(step.StepId, argument.Name)).AppendLine(" = default;");
                    }
                }
            }

            if (NeedsRetryCounter(step))
            {
                loop.Append("int ").Append(RetryCounter(step.StepId));
                loop.AppendLine(durable ? $" = run?.GetRetryCount({EmitText.Quote(step.StepId)}) ?? 0;" : " = 0;");
            }
        }

        loop.AppendLine(durable ? "int __state = run?.Cursor ?? 0;" : "int __state = 0;");
        loop.AppendLine("while (true)");
        loop.AppendLine("{");
        loop.AppendLine("    switch (__state)");
        loop.AppendLine("    {");

        for (int i = 0; i < steps.Count; i++)
        {
            loop.Append("        case ").Append(i.ToString(CultureInfo.InvariantCulture)).AppendLine(":");
            if (steps[i].SubWorkflowId is not null)
            {
                EmitSubWorkflowCase(steps[i], i, stepIndex, options, fields, loop, auxiliaryTypes, stepOutputLocals, selection);
            }
            else if (steps[i].Channel is not null)
            {
                EmitChannelCase(steps[i], i, stepIndex, options, fields, loop, auxiliaryTypes, stepOutputLocals, selection, usesCorrelation);
            }
            else
            {
                EmitCase(steps[i], i, stepIndex, options, fields, loop, auxiliaryTypes, stepOutputLocals, selection);
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
        Dictionary<string, string> stepOutputLocals,
        TransportSelection selection)
    {
        ResolvedOperation operation = step.Operation!.Value;
        string identifier = EmitText.SanitizeIdentifier(step.StepId);
        string prefix = $"{identifier}_";
        string camel = EmitText.ToCamelCase(identifier);
        string clientVar = $"{camel}Client";
        string responseVar = $"{camel}Response";
        string responseBodyLocal = $"{camel}ResponseBody";
        string? bindBodyLocal = step.BindResponseBody ? responseBodyLocal : null;

        bool referencesUrl = StepReferencesUrl(step);
        string urlLocal = RequestUrlEmitter.UrlLocal(prefix);
        var requestContext = new StepRequestContext(
            operation.Operation.Method.ToString().ToUpperInvariant(), step.Arguments, step.RequestBody, referencesUrl ? urlLocal : null);

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

        EmitDispatch(gate, step.OnSuccess, isFailure: false, camel, prefix, responseVar, new CriterionSources(bindBodyLocal), stepIndex, fields, auxiliaryTypes, operation.Operation.ResponseHeaders, requestContext, stepOutputLocals, options, selection);
        gate.AppendLine("}");
        gate.AppendLine("else");
        gate.AppendLine("{");
        EmitDispatch(gate, step.OnFailure, isFailure: true, camel, prefix, responseVar, new CriterionSources(bindBodyLocal), stepIndex, fields, auxiliaryTypes, operation.Operation.ResponseHeaders, requestContext, stepOutputLocals, options, selection);
        gate.AppendLine("}");

        string gateText = gate.ToString();
        bool usesContext = gateText.Contains("context", StringComparison.Ordinal);
        bool urlInlined = referencesUrl && gateText.Contains(urlLocal, StringComparison.Ordinal);

        string gateBody = BuildOperationGateBody(step, operation, usesContext, responseVar, responseBodyLocal, gateText);

        // ---- assemble the case block ----
        var c = new StringBuilder();
        c.AppendLine("{");
        c.Append("    // ── step: ").Append(step.StepId).AppendLine(" ──");
        WorkflowExecutorEmitter.AppendIndented(c, request.Statements, 4);

        // A $url criterion (on the success gate or any onSuccess/onFailure action) resolves against the
        // request's relative URL, rebuilt from the resolved Sources before the client call disposes them.
        // When inlined it compares an executor-owned byte[] (no context); a non-inlined $url (fallback to
        // a CompiledCriterion, which already forces the context) resolves via the context.
        if (urlInlined)
        {
            WorkflowExecutorEmitter.AppendIndented(c, RequestUrlEmitter.Emit(operation, request.ParameterBindings, prefix, "workspace", fields), 4);
        }
        else if (referencesUrl)
        {
            WorkflowExecutorEmitter.AppendIndented(c, RequestUrlEmitter.EmitToContext(operation, request.ParameterBindings, prefix, "workspace", "context"), 4);
        }

        c.Append("    var ").Append(clientVar).Append(" = new ").Append(operation.Operation.ClientTypeName).Append('(').Append(selection.ForSource(operation.SourceName)).AppendLine(");");

        string callToken = EmitTimeoutToken(step, camel, c);
        var callArguments = new List<string>(request.NamedArguments) { $"cancellationToken: {callToken}" };

        c.Append("    int ").Append(camel).Append("Next = ").Append((index + 1).ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        c.Append("    bool ").Append(camel).AppendLine("End = false;");
        c.Append("    bool ").Append(camel).AppendLine("Fail = false;");
        c.Append("    bool ").Append(camel).AppendLine("Retry = false;");
        c.Append("    double ").Append(camel).AppendLine("RetryDelay = 0;");

        if (step.TimeoutMs.HasValue)
        {
            // Timed step: hoist the response so the timed call sits in its own try. An
            // OperationCanceledException raised by the step timeout (and not by the caller's cancellation)
            // routes to the step's onFailure dispatch, evaluated without response context — there is no
            // response — so only $inputs/$steps criteria (typically an unconditional retry/goto) apply.
            var timeoutFailure = new StringBuilder();
            EmitDispatch(timeoutFailure, step.OnFailure, isFailure: true, camel, $"{prefix}to_", string.Empty, default, stepIndex, fields, auxiliaryTypes, null, default, stepOutputLocals, options, selection);

            c.Append("    ").Append(operation.Operation.ResponseTypeName).Append(' ').Append(responseVar).AppendLine(" = default;");
            c.Append("    bool ").Append(camel).AppendLine("Got = false;");
            c.AppendLine("    try");
            c.AppendLine("    {");
            c.Append("        ").Append(responseVar).Append(" = await ").Append(clientVar).Append('.')
                .Append(operation.Operation.ClientMethodName).Append('(').Append(string.Join(", ", callArguments)).AppendLine(").ConfigureAwait(false);");
            c.Append("        ").Append(camel).AppendLine("Got = true;");
            c.AppendLine("    }");
            c.AppendLine("    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)");
            c.AppendLine("    {");
            c.AppendLine("    }");
            WorkflowExecutorEmitter.AppendIndented(c, request.Cleanup, 4);

            c.Append("    if (").Append(camel).AppendLine("Got)");
            c.AppendLine("    {");
            c.AppendLine("        try");
            c.AppendLine("        {");
            WorkflowExecutorEmitter.AppendIndented(c, gateBody, 12);
            c.AppendLine("        }");
            c.AppendLine("        finally");
            c.AppendLine("        {");
            c.Append("            await ").Append(responseVar).AppendLine(".DisposeAsync().ConfigureAwait(false);");
            c.AppendLine("        }");
            c.AppendLine("    }");
            c.AppendLine("    else");
            c.AppendLine("    {");
            WorkflowExecutorEmitter.AppendIndented(c, timeoutFailure.ToString(), 8);
            c.AppendLine("    }");
        }
        else
        {
            c.Append("    var ").Append(responseVar).Append(" = await ").Append(clientVar).Append('.')
                .Append(operation.Operation.ClientMethodName).Append('(').Append(string.Join(", ", callArguments)).AppendLine(").ConfigureAwait(false);");
            WorkflowExecutorEmitter.AppendIndented(c, request.Cleanup, 4);

            c.AppendLine("    try");
            c.AppendLine("    {");
            WorkflowExecutorEmitter.AppendIndented(c, gateBody, 8);
            c.AppendLine("    }");
            c.AppendLine("    finally");
            c.AppendLine("    {");
            c.Append("        await ").Append(responseVar).AppendLine(".DisposeAsync().ConfigureAwait(false);");
            c.AppendLine("    }");
        }

        // Apply the captured control-flow decision now the response is disposed.
        AppendApply(c, step, index, camel, options);
        c.AppendLine("}");

        WorkflowExecutorEmitter.AppendIndented(loop, c.ToString(), 12);
    }

    // Applies the captured control-flow decision now the response is disposed: fail, end (jump to outputs),
    // retry (re-enter the same state), or advance to the next state. In durable mode a checkpoint is written
    // after the next cursor is chosen and before the loop iterates (plan §9.3), so a crash resumes from the
    // last completed step; a fault returns Faulted, and a retry with a delay suspends on a durable timer and
    // returns Suspended (plan §9.4). With a null run a durable executor falls back to the non-durable
    // behaviour (throw on fault, Task.Delay on a retry delay).
    private static void AppendApply(StringBuilder c, in ControlFlowStep step, int index, string camel, in WorkflowExecutorOptions options)
    {
        bool durable = options.Durable;
        string checkpoint = BuildCheckpoint(step, durable);
        string staging = BuildStaging(step, durable);
        string indexLiteral = index.ToString(CultureInfo.InvariantCulture);
        string failMessage = EmitText.Quote($"Step '{step.StepId}' did not satisfy its success criteria.");
        string throwStatement = $"throw new WorkflowStepFailedException({EmitText.Quote(step.StepId)}, {failMessage});";

        // ── fail ──
        if (durable)
        {
            string attempt = NeedsRetryCounter(step) ? RetryCounter(step.StepId) : "1";
            c.Append("    if (").Append(camel).AppendLine("Fail)");
            c.AppendLine("    {");
            c.AppendLine("        if (run is not null)");
            c.AppendLine("        {");
            WorkflowExecutorEmitter.AppendIndented(c, staging, 12);
            c.Append("            WorkflowFault ").Append(camel).Append("Fault = await run.FaultAsync(")
                .Append(EmitText.Quote(step.StepId)).Append(", ").Append(attempt).Append(", ").Append(failMessage)
                .AppendLine(", cancellationToken).ConfigureAwait(false);");
            c.Append("            return WorkflowRunResult<").Append(options.OutputsTypeName).Append(">.Faulted(").Append(camel).AppendLine("Fault);");
            c.AppendLine("        }");
            c.AppendLine();
            c.Append("        ").AppendLine(throwStatement);
            c.AppendLine("    }");
        }
        else
        {
            c.Append("    if (").Append(camel).Append("Fail) { ").Append(throwStatement).AppendLine(" }");
        }

        // ── end ──
        c.Append("    if (").Append(camel).AppendLine("End) { goto __workflowOutputs; }");

        // ── retry ──
        c.Append("    if (").Append(camel).AppendLine("Retry)");
        c.AppendLine("    {");
        if (durable)
        {
            c.Append("        __state = ").Append(indexLiteral).AppendLine(";");
            c.AppendLine("        if (run is not null)");
            c.AppendLine("        {");
            c.Append("            if (").Append(camel).AppendLine("RetryDelay > 0)");
            c.AppendLine("            {");
            WorkflowExecutorEmitter.AppendIndented(c, staging, 16);
            c.Append("                WorkflowWait ").Append(camel).Append("Wait = await run.SuspendForTimerAsync(__state, TimeSpan.FromSeconds(")
                .Append(camel).AppendLine("RetryDelay), cancellationToken).ConfigureAwait(false);");
            c.Append("                return WorkflowRunResult<").Append(options.OutputsTypeName).Append(">.Suspended(").Append(camel).AppendLine("Wait);");
            c.AppendLine("            }");
            c.AppendLine();
            WorkflowExecutorEmitter.AppendIndented(c, checkpoint, 12);
            c.AppendLine("            continue;");
            c.AppendLine("        }");
            c.AppendLine();
            c.Append("        if (").Append(camel).Append("RetryDelay > 0) { await Task.Delay(TimeSpan.FromSeconds(").Append(camel)
                .AppendLine("RetryDelay), timeProvider ?? TimeProvider.System, cancellationToken).ConfigureAwait(false); }");
            c.AppendLine("        continue;");
        }
        else
        {
            c.Append("        if (").Append(camel).Append("RetryDelay > 0) { await Task.Delay(TimeSpan.FromSeconds(").Append(camel)
                .AppendLine("RetryDelay), timeProvider ?? TimeProvider.System, cancellationToken).ConfigureAwait(false); }");
            c.Append("        __state = ").Append(indexLiteral).AppendLine(";");
            c.AppendLine("        continue;");
        }

        c.AppendLine("    }");

        // ── next ──
        c.Append("    __state = ").Append(camel).AppendLine("Next;");
        WorkflowExecutorEmitter.AppendIndented(c, checkpoint, 4);
        c.AppendLine("    continue;");
    }

    // Stages a step's products (outputs + retry counter) on the run, so the next persist (a checkpoint, a
    // suspend, or a fault) serialises them. Empty in non-durable mode.
    private static string BuildStaging(in ControlFlowStep step, bool durable)
    {
        if (!durable)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        if (ProducesOutputs(step))
        {
            sb.Append("run?.SetStepOutputs(").Append(EmitText.Quote(step.StepId)).Append(", ")
                .Append(EmitText.StepOutputsElementLocal(step.StepId)).AppendLine(");");
        }

        if (NeedsRetryCounter(step))
        {
            sb.Append("run?.SetRetryCount(").Append(EmitText.Quote(step.StepId)).Append(", ")
                .Append(RetryCounter(step.StepId)).AppendLine(");");
        }

        return sb.ToString();
    }

    // The durable checkpoint statements for a step: stage its products then persist at the current cursor.
    // Empty in non-durable mode.
    private static string BuildCheckpoint(in ControlFlowStep step, bool durable)
    {
        if (!durable)
        {
            return string.Empty;
        }

        return BuildStaging(step, durable) + "if (run is not null) { await run.CheckpointAsync(__state, cancellationToken).ConfigureAwait(false); }\n";
    }

    // Emits the linked-CTS setup for a step that declares a `timeout` and returns the token expression to
    // thread into the step's async call(s); emits nothing and returns "cancellationToken" for an untimed step.
    private static string EmitTimeoutToken(in ControlFlowStep step, string camel, StringBuilder c)
    {
        if (step.TimeoutMs is not { } milliseconds)
        {
            return "cancellationToken";
        }

        c.Append("    using var ").Append(camel).AppendLine("Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);");
        c.Append("    ").Append(camel).Append("Cts.CancelAfter(").Append(milliseconds.ToString(CultureInfo.InvariantCulture)).AppendLine(");");
        return $"{camel}Cts.Token";
    }

    // True when any of the step's criteria — its success gate or any onSuccess/onFailure action — reference
    // the $url runtime expression.
    private static bool StepReferencesUrl(in ControlFlowStep step)
    {
        if (RequestUrlEmitter.ReferencesUrl(step.SuccessCriteria))
        {
            return true;
        }

        foreach (StepActionInfo action in step.OnSuccess)
        {
            if (RequestUrlEmitter.ReferencesUrl(action.Criteria))
            {
                return true;
            }
        }

        foreach (StepActionInfo action in step.OnFailure)
        {
            if (RequestUrlEmitter.ReferencesUrl(action.Criteria))
            {
                return true;
            }
        }

        return false;
    }

    // Builds the body of an operation step's success-gate try (telemetry, response context, optional response
    // body binding, then the success/onSuccess/onFailure gate) with no base indentation, so the same block
    // can be placed at the timed (deeper) or untimed indent.
    private static string BuildOperationGateBody(
        in ControlFlowStep step,
        in ResolvedOperation operation,
        bool usesContext,
        string responseVar,
        string responseBodyLocal,
        string gateText)
    {
        var b = new StringBuilder();
        b.AppendLine("ArazzoTelemetry.StepsExecuted.Add(1);");
        if (usesContext)
        {
            b.Append("context.SetResponseStatusCode(").Append(responseVar).AppendLine(".StatusCode);");
        }

        if (step.BindResponseBody)
        {
            b.Append("JsonElement ").Append(responseBodyLocal).AppendLine(" = default;");
            foreach (ResponseDescriptor response in operation.Operation.Responses)
            {
                if (response.BodyPropertyName is { } bodyProperty
                    && int.TryParse(response.StatusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out int statusCode))
                {
                    b.Append("if (").Append(responseVar).Append(".StatusCode == ").Append(statusCode.ToString(CultureInfo.InvariantCulture))
                        .Append(") { ").Append(responseBodyLocal).Append(" = (JsonElement)").Append(responseVar).Append('.').Append(bodyProperty).AppendLine("; }");
                }
            }

            if (usesContext)
            {
                b.Append("context.SetResponseBody(").Append(responseBodyLocal).AppendLine(");");
            }
        }

        b.Append(gateText);
        return b.ToString();
    }

    private static void EmitSubWorkflowCase(
        in ControlFlowStep step,
        int index,
        IReadOnlyDictionary<string, int> stepIndex,
        in WorkflowExecutorOptions options,
        StringBuilder fields,
        StringBuilder loop,
        StringBuilder auxiliaryTypes,
        Dictionary<string, string> stepOutputLocals,
        TransportSelection selection)
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
        EmitDispatch(successDispatch, step.OnSuccess, isFailure: false, camel, prefix, string.Empty, default, stepIndex, fields, auxiliaryTypes, null, default, stepOutputLocals, options, selection);
        var failureDispatch = new StringBuilder();
        EmitDispatch(failureDispatch, step.OnFailure, isFailure: true, camel, prefix, string.Empty, default, stepIndex, fields, auxiliaryTypes, null, default, stepOutputLocals, options, selection);

        var inputs = new StringBuilder();
        string builderVariable = SubWorkflowStepEmitter.BuildInputs(fields, inputs, step.StepId, step.Arguments, stepOutputLocals, "inputs", options.InputAccessors, out IReadOnlyDictionary<string, string> inputValueLocals);
        string targetClass = SubWorkflowStepEmitter.TargetClass(
            WorkflowExecutorEmitter.ResolveSubWorkflowNamespace(options, step.SubWorkflowSource), subWorkflowId);

        var c = new StringBuilder();
        c.AppendLine("{");
        c.Append("    // ── step: ").Append(step.StepId).Append(" (workflow: ").Append(subWorkflowId).AppendLine(") ──");
        WorkflowExecutorEmitter.AppendIndented(c, inputs.ToString(), 4);

        // Expose each passed input value for $workflows.<id>.inputs.<name> by copying the (stable) resolved
        // argument value into its hoisted local, which survives across the loop. The builder's own RootElement
        // is not captured: its backing document does not survive the sub-workflow call.
        foreach (KeyValuePair<string, string> inputValueLocal in inputValueLocals)
        {
            c.Append("    ").Append(SubWorkflowInputVar(step.StepId, inputValueLocal.Key)).Append(" = ").Append(inputValueLocal.Value).AppendLine(";");
        }

        c.Append("    int ").Append(camel).Append("Next = ").Append((index + 1).ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        c.Append("    bool ").Append(camel).AppendLine("End = false;");
        c.Append("    bool ").Append(camel).AppendLine("Fail = false;");
        c.Append("    bool ").Append(camel).AppendLine("Retry = false;");
        c.Append("    double ").Append(camel).AppendLine("RetryDelay = 0;");
        c.Append("    bool ").Append(completedLocal).AppendLine(";");
        string subToken = EmitTimeoutToken(step, camel, c);

        // The sub-workflow fails by throwing WorkflowStepFailedException; catch it so onFailure can act.
        c.AppendLine("    try");
        c.AppendLine("    {");
        c.AppendLine("        ArazzoTelemetry.StepsExecuted.Add(1);");
        c.Append("        ").Append(outputsLocal).Append(" = await ").Append(targetClass)
            .Append(".ExecuteAsync(").Append(selection.SubWorkflowArgument).Append(", workspace, ").Append(builderVariable).Append(".RootElement, ").Append(subToken).AppendLine(").ConfigureAwait(false);");

        // Expose the sub-workflow's outputs for $workflows.<id>.outputs runtime expressions (plan §11) — so a
        // later step's criteria/parameters or the workflow outputs can reference this invocation's results.
        c.Append("        context.SetWorkflowOutputs(").Append(EmitText.Quote(subWorkflowId)).Append(", ").Append(outputsLocal).AppendLine(");");
        c.Append("        ").Append(completedLocal).AppendLine(" = true;");
        c.AppendLine("    }");
        c.AppendLine("    catch (WorkflowStepFailedException)");
        c.AppendLine("    {");
        c.Append("        ").Append(completedLocal).AppendLine(" = false;");
        c.AppendLine("    }");
        if (step.TimeoutMs.HasValue)
        {
            // A step timeout (not the caller's cancellation) is a step failure, not a workflow cancellation.
            c.AppendLine("    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)");
            c.AppendLine("    {");
            c.Append("        ").Append(completedLocal).AppendLine(" = false;");
            c.AppendLine("    }");
        }

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

        AppendApply(c, step, index, camel, options);
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
        Dictionary<string, string> stepOutputLocals,
        TransportSelection selection,
        bool usesCorrelation)
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
        EmitDispatch(successDispatch, step.OnSuccess, isFailure: false, camel, prefix, string.Empty, default, stepIndex, fields, auxiliaryTypes, null, default, stepOutputLocals, options, selection);
        var failureDispatch = new StringBuilder();
        EmitDispatch(failureDispatch, step.OnFailure, isFailure: true, camel, prefix, string.Empty, default, stepIndex, fields, auxiliaryTypes, null, default, stepOutputLocals, options, selection);

        var c = new StringBuilder();
        c.AppendLine("{");
        c.Append("    // ── step: ").Append(step.StepId).AppendLine(" (channel) ──");
        c.Append("    int ").Append(camel).Append("Next = ").Append((index + 1).ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        c.Append("    bool ").Append(camel).AppendLine("End = false;");
        c.Append("    bool ").Append(camel).AppendLine("Fail = false;");
        c.Append("    bool ").Append(camel).AppendLine("Retry = false;");
        c.Append("    double ").Append(camel).AppendLine("RetryDelay = 0;");
        c.Append("    bool ").Append(camel).AppendLine("Success = false;");

        string channelToken = EmitTimeoutToken(step, camel, c);
        bool timed = step.TimeoutMs.HasValue;

        // For a timed step the receive/send is built into a buffer and wrapped in a try below; an untimed
        // step writes straight into the case body (byte-identical to the pre-timeout output).
        StringBuilder work = timed ? new StringBuilder() : c;

        if (isReceive)
        {
            EmitReceiveBody(work, step, index, descriptor, camel, prefix, fields, auxiliaryTypes, stepOutputLocals, options, channelToken);
        }
        else
        {
            // Send: publish the payload, then the step succeeds unconditionally. (Request/reply with
            // actions is rejected up front, so this is always a fire-and-forget publish.)
            string send = SendChannelStepEmitter.Emit(
                step.StepId, channel, step.RequestBody, step.Outputs, step.SuccessCriteria, step.Arguments, "messageTransport", "workspace",
                stepOutputLocals, "inputs", options.InputAccessors, fields, auxiliaryTypes, options.Namespace, channelToken, captureCorrelation: usesCorrelation);
            WorkflowExecutorEmitter.AppendIndented(work, send, 4);
            work.Append("    ").Append(camel).AppendLine("Success = true;");
        }

        if (timed)
        {
            // A step timeout (not the caller's cancellation) leaves Success false so the onFailure dispatch
            // below handles it — for a receive the message simply never arrived in time.
            c.AppendLine("    try");
            c.AppendLine("    {");
            WorkflowExecutorEmitter.AppendIndented(c, work.ToString(), 4);
            c.AppendLine("    }");
            c.AppendLine("    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)");
            c.AppendLine("    {");
            c.AppendLine("    }");
        }

        c.Append("    if (").Append(camel).AppendLine("Success)");
        c.AppendLine("    {");
        WorkflowExecutorEmitter.AppendIndented(c, successDispatch.ToString(), 8);
        c.AppendLine("    }");
        c.AppendLine("    else");
        c.AppendLine("    {");
        WorkflowExecutorEmitter.AppendIndented(c, failureDispatch.ToString(), 8);
        c.AppendLine("    }");

        AppendApply(c, step, index, camel, options);
        c.AppendLine("}");

        WorkflowExecutorEmitter.AppendIndented(loop, c.ToString(), 12);
    }

    // Emits the receive into the case body: the message is handled inline (while the payload is live) to
    // evaluate the success criteria and project outputs; the success flag and outputs element escape to
    // the surrounding case for action dispatch.
    private static void EmitReceiveBody(
        StringBuilder c,
        in ControlFlowStep step,
        int index,
        in AsyncApiChannelDescriptor descriptor,
        string camel,
        string prefix,
        StringBuilder fields,
        StringBuilder auxiliaryTypes,
        Dictionary<string, string> stepOutputLocals,
        in WorkflowExecutorOptions options,
        string cancellationTokenExpression)
    {
        ReceiveChannelStepEmitter.ValidateCriteria(step.StepId, step.SuccessCriteria);

        string payloadType = descriptor.Messages.Count > 0 && descriptor.Messages[0].PayloadTypeName is { } typeName
            ? typeName
            : "Corvus.Text.Json.JsonElement";
        bool isResponder = descriptor.ReplyPayloadTypeName is not null;
        string payloadLocal = $"{camel}MessagePayload";
        string outputsLocal = EmitText.StepOutputsElementLocal(step.StepId);

        // The success gate + output projection, written to run against a live payload that is already in
        // scope as `JsonElement {payloadLocal}` (and `JsonElement messageHeaders`). It is shared by the
        // subscription handler (Tier-1 blocking receive) and the durable resume-with-delivered-message path.
        var sources = new CriterionSources(payloadLocal, "messageHeaders");
        var gateOps = new StringBuilder();
        string gateExpression = step.SuccessCriteria.Count == 0
            ? "true"
            : StepBodyEmitter.EmitCriteriaExpression(
                step.SuccessCriteria, fields, gateOps, auxiliaryTypes, $"{prefix}recv", "context",
                responseVar: string.Empty, sources, "inputs", stepOutputLocals, options.InputAccessors, null, default, options.Namespace);
        bool usesContext = ReceiveChannelStepEmitter.UsesContext(gateOps, gateExpression);

        var handleCore = new StringBuilder();
        if (usesContext)
        {
            handleCore.Append("context.SetMessagePayload(").Append(payloadLocal).AppendLine(");");
        }

        handleCore.Append(gateOps);
        handleCore.Append(camel).Append("Success = (").Append(gateExpression).AppendLine(");");
        handleCore.Append("if (").Append(camel).AppendLine("Success)");
        handleCore.AppendLine("{");
        if (step.Outputs.Count > 0)
        {
            OutputExtractionCode outputCode = OutputExtractionEmitter.Emit(
                step.StepId, step.Outputs, "workspace", "context", stepOutputLocals, "inputs", options.InputAccessors, responseBodyLocal: null, messagePayloadLocal: payloadLocal, messageHeadersLocal: "messageHeaders");
            fields.Append(outputCode.Fields);
            WorkflowExecutorEmitter.AppendIndented(handleCore, outputCode.Statements, 4);
        }
        else
        {
            handleCore.Append("    ").Append(outputsLocal).Append(" = ").Append(payloadLocal).AppendLine(".CloneAsBuilder(workspace).RootElement;");
        }

        handleCore.AppendLine("}");
        string handleCoreText = handleCore.ToString();

        // The subscription handler reads the typed payload as a JsonElement, then runs the shared core.
        var lambdaBody = new StringBuilder();
        lambdaBody.Append("JsonElement ").Append(payloadLocal).AppendLine(" = JsonElement.From(message);");
        lambdaBody.Append(handleCoreText);

        c.AppendLine("    ArazzoTelemetry.StepsExecuted.Add(1);");

        // The subscription address — a constant for a static channel, or built at runtime from the step's
        // parameters for a parameterised channel (emitted into the case body, indented).
        var addressStatements = new StringBuilder();
        string address = ChannelAddressEmitter.EmitReceiveAddress(
            descriptor, step.Arguments, fields, addressStatements, $"{prefix}ch", stepOutputLocals, "inputs", options.InputAccessors, out string channelText);
        WorkflowExecutorEmitter.AppendIndented(c, addressStatements.ToString(), 4);

        if (isResponder)
        {
            // Request/reply responder: resolve and return the reply (the step's requestBody) regardless of
            // the success gate — the responder received a request and must reply; the gate only governs the
            // step's success (and thus its onSuccess/onFailure dispatch) after the reply has been sent. A
            // responder is a synchronous reply, so it does not suspend even in durable mode.
            string replyType = descriptor.ReplyPayloadTypeName!;
            string replyExpression = ReceiveChannelStepEmitter.EmitReplyResolution(
                step.StepId, step.RequestBody, replyType, payloadLocal, "workspace", $"{prefix}reply", "inputs", stepOutputLocals, options.InputAccessors, fields, lambdaBody);
            lambdaBody.Append("return new ValueTask<").Append(replyType).Append(">(").Append(replyExpression).AppendLine(");");

            c.Append("    await messageTransport.ReceiveOneAndReplyAsync<").Append(payloadType).Append(", ").Append(replyType).Append(">(")
                .Append(address).AppendLine(", (message, messageHeaders) =>");
            c.AppendLine("    {");
            WorkflowExecutorEmitter.AppendIndented(c, lambdaBody.ToString(), 8);
            c.Append("    }, ").Append(cancellationTokenExpression).AppendLine(").ConfigureAwait(false);");
            return;
        }

        lambdaBody.AppendLine("return default;");

        string? correlationName = step.CorrelationName;
        string correlationLocation = step.CorrelationLocation ?? string.Empty;
        string expectedLocal = $"{camel}Expected";

        // Emits the Tier-1 blocking receive (subscribe and wait inline) into the given builder at the case
        // body indent — used directly when not durable, and as the run-less fallback in durable mode.
        void EmitBlockingReceive(StringBuilder t)
        {
            if (correlationName is not null)
            {
                t.Append("    if (correlationTokens.TryGetValue(").Append(EmitText.Quote(correlationName)).Append(", out byte[]? ").Append(expectedLocal).AppendLine("))");
                t.AppendLine("    {");
                t.Append("        await messageTransport.ReceiveOneAsync<").Append(payloadType).Append(">(")
                    .Append(address).AppendLine(", (message, messageHeaders) =>");
                t.AppendLine("        {");
                WorkflowExecutorEmitter.AppendIndented(t, lambdaBody.ToString(), 12);
                t.Append("        }, ").Append(cancellationTokenExpression)
                    .Append(", (message, messageHeaders) => CorrelationToken.Matches(JsonElement.From(message), ")
                    .Append(EmitText.Quote(correlationLocation)).Append("u8, ").Append(expectedLocal).AppendLine(")).ConfigureAwait(false);");
                t.AppendLine("    }");
            }
            else
            {
                t.Append("    await messageTransport.ReceiveOneAsync<").Append(payloadType).Append(">(")
                    .Append(address).AppendLine(", (message, messageHeaders) =>");
                t.AppendLine("    {");
                WorkflowExecutorEmitter.AppendIndented(t, lambdaBody.ToString(), 8);
                t.Append("    }, ").Append(cancellationTokenExpression).AppendLine(").ConfigureAwait(false);");
            }
        }

        // Durable suspension applies to a static-address, non-responder receive: with a run present the step
        // either consumes a message a worker delivered (resume) or checkpoints a message wait and returns
        // Suspended (plan §9.4). A null run, a dynamic/parameterised address, or a responder keeps the
        // Tier-1 blocking receive.
        // A parameterised address suspends durably on its resolved value (channelText); only a truly dynamic
        // (empty-template) address, or a null run / responder, keeps the Tier-1 blocking receive.
        bool supportsSuspend = options.Durable && !descriptor.IsDynamicAddress;
        if (!supportsSuspend)
        {
            EmitBlockingReceive(c);
            return;
        }

        string deliveredLocal = $"{camel}Delivered";
        string indexLiteral = index.ToString(CultureInfo.InvariantCulture);
        string correlationIdExpression = correlationName is not null
            ? $"System.Text.Encoding.UTF8.GetString({expectedLocal})"
            : "null";

        var delivered = new StringBuilder();
        delivered.Append("JsonElement ").Append(payloadLocal).Append(" = ").Append(deliveredLocal).AppendLine(";");
        if (handleCoreText.Contains("messageHeaders", StringComparison.Ordinal))
        {
            delivered.AppendLine("JsonElement messageHeaders = default;");
        }

        delivered.Append(handleCoreText);

        c.AppendLine("    if (run is not null)");
        c.AppendLine("    {");
        c.Append("        if (run.TryTakeDeliveredMessage(out JsonElement ").Append(deliveredLocal).AppendLine("))");
        c.AppendLine("        {");
        WorkflowExecutorEmitter.AppendIndented(c, delivered.ToString(), 12);
        c.AppendLine("        }");
        if (correlationName is not null)
        {
            // Suspend awaiting the correlated reply only once the request was actually published (a token is
            // registered); with no token the receive cannot match, so Success stays false → onFailure.
            c.Append("        else if (correlationTokens.TryGetValue(").Append(EmitText.Quote(correlationName)).Append(", out byte[]? ").Append(expectedLocal).AppendLine("))");
            c.AppendLine("        {");
            c.Append("            WorkflowWait ").Append(camel).Append("Wait = await run.SuspendForMessageAsync(").Append(indexLiteral).Append(", ").Append(channelText).Append(", ").Append(correlationIdExpression).AppendLine(", cancellationToken).ConfigureAwait(false);");
            c.Append("            return WorkflowRunResult<").Append(options.OutputsTypeName).Append(">.Suspended(").Append(camel).AppendLine("Wait);");
            c.AppendLine("        }");
        }
        else
        {
            c.AppendLine("        else");
            c.AppendLine("        {");
            c.Append("            WorkflowWait ").Append(camel).Append("Wait = await run.SuspendForMessageAsync(").Append(indexLiteral).Append(", ").Append(channelText).Append(", ").Append(correlationIdExpression).AppendLine(", cancellationToken).ConfigureAwait(false);");
            c.Append("            return WorkflowRunResult<").Append(options.OutputsTypeName).Append(">.Suspended(").Append(camel).AppendLine("Wait);");
            c.AppendLine("        }");
        }

        c.AppendLine("    }");
        c.AppendLine("    else");
        c.AppendLine("    {");
        EmitBlockingReceive(c);
        c.AppendLine("    }");
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

    /// <summary>
    /// The hoisted local that exposes a sub-workflow step's bound argument value for
    /// <c>$workflows.&lt;id&gt;.inputs.&lt;name&gt;</c> across the control-flow loop.
    /// </summary>
    private static string SubWorkflowInputVar(string stepId, string parameterName)
        => EmitText.ToCamelCase(EmitText.SanitizeIdentifier(stepId)) + "Input_" + EmitText.SanitizeIdentifier(parameterName) + "Value";

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
        in WorkflowExecutorOptions options,
        TransportSelection selection)
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

            resolved.Add((expression, BuildApply(action, camel, stepIndex, options, selection)));
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

    private static string BuildApply(in StepActionInfo action, string camel, IReadOnlyDictionary<string, int> stepIndex, in WorkflowExecutorOptions options, TransportSelection selection)
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
                    // to the target's inputs type via its implicit operator). A cross-document target
                    // ($sourceDescriptions.<name>.<workflowId>) resolves to its per-source namespace.
                    string targetNamespace = WorkflowExecutorEmitter.ResolveSubWorkflowNamespace(options, action.TargetWorkflowSource);
                    return $"return await {SubWorkflowStepEmitter.TargetClass(targetNamespace, targetWorkflow)}.ExecuteAsync({selection.SubWorkflowArgument}, workspace, (JsonElement)inputs, cancellationToken).ConfigureAwait(false);\n";
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