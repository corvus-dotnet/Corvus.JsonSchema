// <copyright file="WorkflowExecutorEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo11;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits a complete static executor class for an Arazzo workflow (plan §3.1): a single
/// <c>ExecuteAsync</c> that runs the steps sequentially, composing the request-binding, step-body,
/// and output-extraction emitters, and returns the workflow outputs.
/// </summary>
/// <remarks>
/// The caller supplies the <c>JsonWorkspace</c> (it owns every document built during the run — step
/// outputs and the returned outputs — for the whole call). Reads the steps' parameters, criteria, and
/// outputs directly from the strongly-typed <see cref="ArazzoDocument"/>; the binder supplies only the
/// operation→generated-type mapping. Operation steps are supported; sub-workflow and channel steps are
/// a later phase.
/// </remarks>
public static class WorkflowExecutorEmitter
{
    /// <summary>
    /// Emits the executor class source for a workflow.
    /// </summary>
    /// <param name="workflow">The workflow to emit.</param>
    /// <param name="binder">The operation binder for the document's source descriptions.</param>
    /// <param name="options">The emission options (namespace, class name, inputs/outputs type names).</param>
    /// <param name="components">The document's <c>components</c> object, for resolving reusable <c>$ref</c> actions and parameters; <see langword="default"/> when there are none.</param>
    /// <returns>The C# source of the generated executor class.</returns>
    public static string Emit(
        in ArazzoDocument.WorkflowObject workflow,
        WorkflowOperationBinder binder,
        in WorkflowExecutorOptions options,
        JsonElement components = default)
    {
        ArgumentNullException.ThrowIfNull(binder);

        string workflowId = workflow.WorkflowId.IsNotUndefined() ? workflow.WorkflowId.GetString()! : string.Empty;

        var fields = new StringBuilder();
        var body = new StringBuilder();
        var auxiliaryTypes = new StringBuilder();

        // Maps a (preceding) step id to the local that holds its built outputs object, so
        // $steps.<id>.outputs references resolve statically — no runtime dictionary.
        var stepOutputLocals = new Dictionary<string, string>(StringComparer.Ordinal);

        // Bind every step up front; a workflow that declares any onSuccess/onFailure action is emitted
        // as a labelled-loop executor (control flow), otherwise as the straight-line form.
        var boundSteps = new List<ControlFlowStep>();
        bool usesControlFlow = false;

        // Workflow-level success/failure actions apply to every step as defaults (a step's own action
        // with the same name overrides). Both these and a step's actions may be reusable $ref entries.
        List<StepActionInfo> workflowSuccessActions = ReadActions(workflow.SuccessActions, components);
        List<StepActionInfo> workflowFailureActions = ReadActions(workflow.FailureActions, components);

        foreach (ArazzoDocument.StepObject step in workflow.Steps.EnumerateArray())
        {
            string stepId = step.StepId.IsNotUndefined() ? step.StepId.GetString()! : throw new InvalidOperationException("A step is missing its required stepId.");
            StepBinding binding = binder.Bind(step);

            List<OutputMapping> stepOutputs = ReadOutputs(step);
            List<StepCriterion> criteria = ReadCriteria(step);
            List<StepActionInfo> onSuccess = MergeActions(ReadActions(step.OnSuccess, components), workflowSuccessActions);
            List<StepActionInfo> onFailure = MergeActions(ReadActions(step.OnFailure, components), workflowFailureActions);

            // A sub-workflow step invokes another generated workflow. Its criteria (success and action)
            // run against the sub-workflow's outputs / the inputs — never an HTTP response — so they may
            // only reference $inputs and $steps; an action or criterion makes the workflow control-flow.
            if (binding.Kind == StepTargetKind.WorkflowId && binding.SubWorkflowId is { } subWorkflowId)
            {
                ValidateSubWorkflowCriteria(stepId, criteria, onSuccess, onFailure);
                usesControlFlow |= criteria.Count > 0 || onSuccess.Count > 0 || onFailure.Count > 0;
                boundSteps.Add(new ControlFlowStep(stepId, null, ReadArguments(step, components), criteria, stepOutputs, null, false, onSuccess, onFailure, subWorkflowId));
                continue;
            }

            if (binding.Kind is not (StepTargetKind.OperationId or StepTargetKind.OperationPath) || binding.Operation is not { } operation)
            {
                throw new InvalidOperationException(
                    $"Step '{stepId}' targets {binding.Kind}; only operation and sub-workflow steps are supported by the current generator.");
            }

            usesControlFlow |= onSuccess.Count > 0 || onFailure.Count > 0;

            // Only clone the response body into the workspace when the step actually consumes it
            // (success criteria, outputs, or an action's criteria).
            bool bindResponseBody = ReferencesResponseBody(criteria, stepOutputs, onSuccess, onFailure);

            boundSteps.Add(new ControlFlowStep(
                stepId, operation, ReadArguments(step, components), criteria, stepOutputs, ReadRequestBody(step), bindResponseBody, onSuccess, onFailure));
        }

        if (usesControlFlow)
        {
            ControlFlowEmitter.Emit(boundSteps, workflow, options, fields, body, auxiliaryTypes, stepOutputLocals);
        }
        else
        {
            foreach (ControlFlowStep step in boundSteps)
            {
                body.Append("            // ── step: ").Append(step.StepId).AppendLine(" ──");

                if (step.SubWorkflowId is { } subWorkflowId)
                {
                    SubWorkflowStepCode subStep = SubWorkflowStepEmitter.Emit(
                        step.StepId, subWorkflowId, step.Arguments, options.Namespace, stepOutputLocals, "inputs", options.InputAccessors);
                    fields.Append(subStep.Fields);
                    AppendIndented(body, subStep.Statements, 12);
                    stepOutputLocals[step.StepId] = EmitText.StepOutputsElementLocal(step.StepId);
                    body.AppendLine();
                    continue;
                }

                // The step body builds the step's outputs product inside the step (while the response
                // is alive), so output extraction is not a separate post-step pass.
                StepBodyCode stepBody = StepBodyEmitter.Emit(
                    step.StepId, step.Operation!.Value, step.Arguments, step.SuccessCriteria, step.Outputs, "transport", "workspace", "context", "cancellationToken", stepOutputLocals, "inputs", options.InputAccessors, options.Namespace, step.RequestBody, step.BindResponseBody);
                fields.Append(stepBody.Fields);
                AppendIndented(body, stepBody.Statements, 12);
                auxiliaryTypes.Append(stepBody.AuxiliaryTypes);

                if (step.Outputs.Count > 0)
                {
                    stepOutputLocals[step.StepId] = EmitText.StepOutputsElementLocal(step.StepId);
                }

                body.AppendLine();
            }

            AppendWorkflowOutputs(fields, body, workflow, stepOutputLocals, options.InputAccessors);
        }

        string bodyText = body.ToString();

        // The WorkflowExecutionContext is created only when something still resolves through it — a
        // non-inlined criterion or a context-fallback output/workflow-output. Once every criterion is
        // inlined and every value resolves statically, the context leaves the value path entirely.
        bool needsContext = bodyText.Contains("context", StringComparison.Ordinal);

        return Compose(options, workflowId, fields.ToString(), bodyText, auxiliaryTypes.ToString(), needsContext);
    }

    private static List<StepArgument> ReadArguments(in ArazzoDocument.StepObject step, in JsonElement components)
    {
        var arguments = new List<StepArgument>();
        if (step.Parameters.IsNotUndefined())
        {
            foreach (JsonElement element in step.Parameters.EnumerateArray())
            {
                // A reusable-parameter reference ({reference:"$components.parameters.x", value?:…}):
                // resolve the component parameter (name + value), letting a value on the reference
                // override the component's value.
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("reference"u8, out JsonElement referenceElement) && referenceElement.ValueKind == JsonValueKind.String)
                {
                    string reference = referenceElement.GetString()!;
                    if (ResolveComponentReference(components, reference) is not { } resolved
                        || !resolved.TryGetProperty("name"u8, out JsonElement resolvedName) || resolvedName.ValueKind != JsonValueKind.String)
                    {
                        throw new InvalidOperationException($"Could not resolve reusable parameter reference '{reference}'.");
                    }

                    JsonElement valueSource = element.TryGetProperty("value"u8, out JsonElement overrideValue)
                        ? overrideValue
                        : resolved.TryGetProperty("value"u8, out JsonElement componentValue) ? componentValue : default;
                    if (valueSource.ValueKind == JsonValueKind.Undefined)
                    {
                        // No value on the reference or the component — nothing to bind.
                        continue;
                    }

                    ArgumentValueKind referencedKind = Classify(valueSource, out string referencedText);
                    arguments.Add(new StepArgument(resolvedName.GetString()!, referencedText, referencedKind));
                    continue;
                }

                ArazzoDocument.ParameterObject parameter = element;
                if (!parameter.Name.IsNotUndefined())
                {
                    continue;
                }

                string name = parameter.Name.GetString()!;
                ArgumentValueKind kind = Classify(parameter.Value, out string text);
                arguments.Add(new StepArgument(name, text, kind));
            }
        }

        return arguments;
    }

    /// <summary>
    /// Classifies a parameter/payload value as a runtime expression, an interpolation template, or a
    /// constant of a particular JSON kind, returning the text the emitter needs (the expression/template,
    /// the unescaped string content, or the raw JSON).
    /// </summary>
    private static ArgumentValueKind Classify(in JsonElement value, out string text)
    {
        if (value.ValueKind == JsonValueKind.String && value.GetString() is { } s)
        {
            if (s.Contains("{$", StringComparison.Ordinal))
            {
                text = s;
                return ArgumentValueKind.Interpolation;
            }

            if (s.StartsWith('$'))
            {
                text = s;
                return ArgumentValueKind.Expression;
            }

            text = s;
            return ArgumentValueKind.LiteralString;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                text = value.GetRawText();
                return ArgumentValueKind.LiteralNumber;
            case JsonValueKind.True:
                text = "true";
                return ArgumentValueKind.LiteralBoolean;
            case JsonValueKind.False:
                text = "false";
                return ArgumentValueKind.LiteralBoolean;
            case JsonValueKind.Null:
                text = string.Empty;
                return ArgumentValueKind.LiteralNull;
            default:
                text = value.GetRawText();
                return ArgumentValueKind.LiteralComposite;
        }
    }

    private static bool ReferencesResponseBody(
        IReadOnlyList<StepCriterion> criteria,
        IReadOnlyList<OutputMapping> outputs,
        IReadOnlyList<StepActionInfo> onSuccess,
        IReadOnlyList<StepActionInfo> onFailure)
    {
        const string token = "$response.body";

        static bool AnyCriterion(IReadOnlyList<StepCriterion> criteria, string token)
        {
            foreach (StepCriterion criterion in criteria)
            {
                if (criterion.Condition.Contains(token, StringComparison.Ordinal)
                    || (criterion.Context is { } context && context.Contains(token, StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }

        if (AnyCriterion(criteria, token))
        {
            return true;
        }

        foreach (OutputMapping output in outputs)
        {
            if (output.Expression.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (StepActionInfo action in onSuccess)
        {
            if (AnyCriterion(action.Criteria, token))
            {
                return true;
            }
        }

        foreach (StepActionInfo action in onFailure)
        {
            if (AnyCriterion(action.Criteria, token))
            {
                return true;
            }
        }

        return false;
    }

    private static StepBody? ReadRequestBody(in ArazzoDocument.StepObject step)
    {
        if (!step.RequestBody.IsNotUndefined())
        {
            return null;
        }

        ArazzoDocument.RequestBodyObject requestBody = step.RequestBody;
        if (!requestBody.Payload.IsNotUndefined())
        {
            return null;
        }

        // Payload replacements need substitution into the payload — a later phase.
        if (requestBody.Replacements.IsNotUndefined())
        {
            return null;
        }

        ArgumentValueKind kind = Classify(requestBody.Payload, out string text);

        // A composite (object/array) literal that contains embedded runtime expressions needs
        // substitution, which is a later phase — defer it (conservatively, any '$' in its raw JSON).
        if (kind == ArgumentValueKind.LiteralComposite && requestBody.Payload.GetRawText().Contains('$'))
        {
            return null;
        }

        return new StepBody(text, kind);
    }

    private static List<StepCriterion> ReadCriteria(in ArazzoDocument.StepObject step)
        => step.SuccessCriteria.IsNotUndefined() ? ReadCriteriaArray(step.SuccessCriteria) : [];

    private static List<StepCriterion> ReadCriteriaArray(in JsonElement criteriaArray)
    {
        var criteria = new List<StepCriterion>();
        if (criteriaArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement element in criteriaArray.EnumerateArray())
            {
                ArazzoDocument.CriterionObject criterion = element;
                string condition = criterion.Condition.GetString()!;
                string? context = criterion.Context.IsNotUndefined() ? criterion.Context.GetString() : null;
                criteria.Add(new StepCriterion(ResolveCriterionType(criterion.Type), condition, context));
            }
        }

        return criteria;
    }

    /// <summary>
    /// Reads an <c>onSuccess</c>/<c>onFailure</c> (or workflow-level <c>successActions</c>/<c>failureActions</c>)
    /// array into a list of <see cref="StepActionInfo"/>. Inline action objects are read directly; a
    /// reusable-action reference (<c>{reference:"$components.successActions.x"}</c>) is resolved against
    /// <paramref name="components"/>.
    /// </summary>
    private static List<StepActionInfo> ReadActions(in JsonElement actions, in JsonElement components)
    {
        var list = new List<StepActionInfo>();
        if (actions.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (JsonElement entity in actions.EnumerateArray())
        {
            if (entity.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (entity.TryGetProperty("reference"u8, out JsonElement referenceElement) && referenceElement.ValueKind == JsonValueKind.String)
            {
                string reference = referenceElement.GetString()!;
                if (ResolveComponentReference(components, reference) is not { } resolved || ReadInlineAction(resolved) is not { } resolvedAction)
                {
                    throw new InvalidOperationException($"Could not resolve reusable action reference '{reference}'.");
                }

                list.Add(resolvedAction);
                continue;
            }

            if (ReadInlineAction(entity) is { } action)
            {
                list.Add(action);
            }
        }

        return list;
    }

    /// <summary>Reads a single inline success/failure action object, or <see langword="null"/> if it lacks the required name/type.</summary>
    private static StepActionInfo? ReadInlineAction(in JsonElement entity)
    {
        if (!entity.TryGetProperty("name"u8, out JsonElement nameElement) || nameElement.ValueKind != JsonValueKind.String
            || !entity.TryGetProperty("type"u8, out JsonElement typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        StepActionKind kind = typeElement.GetString() switch
        {
            "end" => StepActionKind.End,
            "goto" => StepActionKind.Goto,
            "retry" => StepActionKind.Retry,
            _ => StepActionKind.End,
        };

        string? targetStepId = entity.TryGetProperty("stepId"u8, out JsonElement stepIdElement) && stepIdElement.ValueKind == JsonValueKind.String
            ? stepIdElement.GetString() : null;
        string? targetWorkflowId = entity.TryGetProperty("workflowId"u8, out JsonElement workflowIdElement) && workflowIdElement.ValueKind == JsonValueKind.String
            ? workflowIdElement.GetString() : null;
        double? retryAfter = entity.TryGetProperty("retryAfter"u8, out JsonElement retryAfterElement) && retryAfterElement.ValueKind == JsonValueKind.Number
            ? retryAfterElement.GetDouble() : null;
        int? retryLimit = entity.TryGetProperty("retryLimit"u8, out JsonElement retryLimitElement) && retryLimitElement.ValueKind == JsonValueKind.Number
            ? retryLimitElement.GetInt32() : null;
        List<StepCriterion> criteria = entity.TryGetProperty("criteria"u8, out JsonElement criteriaElement)
            ? ReadCriteriaArray(criteriaElement) : [];

        return new StepActionInfo(nameElement.GetString()!, kind, targetStepId, targetWorkflowId, retryAfter, retryLimit, criteria);
    }

    /// <summary>
    /// Resolves a <c>$components.&lt;section&gt;.&lt;name&gt;</c> reusable-action reference against the
    /// document's <c>components</c> object, returning the referenced action object or <see langword="null"/>.
    /// </summary>
    private static JsonElement? ResolveComponentReference(in JsonElement components, string reference)
    {
        const string prefix = "$components.";
        if (components.ValueKind != JsonValueKind.Object || !reference.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        string rest = reference[prefix.Length..];
        int dot = rest.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0)
        {
            return null;
        }

        string section = rest[..dot];
        string name = rest[(dot + 1)..];

        foreach (JsonProperty<JsonElement> sectionProperty in components.EnumerateObject())
        {
            if (sectionProperty.Name == section && sectionProperty.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> actionProperty in sectionProperty.Value.EnumerateObject())
                {
                    if (actionProperty.Name == name)
                    {
                        return actionProperty.Value;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Merges workflow-level default actions into a step's actions: the step's own actions take
    /// precedence (matched first), then any workflow-level action whose name the step did not override.
    /// </summary>
    private static List<StepActionInfo> MergeActions(List<StepActionInfo> stepActions, IReadOnlyList<StepActionInfo> workflowDefaults)
    {
        if (workflowDefaults.Count == 0)
        {
            return stepActions;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (StepActionInfo action in stepActions)
        {
            names.Add(action.Name);
        }

        foreach (StepActionInfo action in workflowDefaults)
        {
            if (names.Add(action.Name))
            {
                stepActions.Add(action);
            }
        }

        return stepActions;
    }

    /// <summary>
    /// Validates that a sub-workflow step's criteria (success and action) reference only sources that
    /// exist for a workflow invocation — <c>$inputs</c> and <c>$steps</c>. A sub-workflow step has no
    /// HTTP response or request, so <c>$statusCode</c>/<c>$response</c>/<c>$request</c>/<c>$method</c>/<c>$url</c>
    /// cannot resolve and are rejected up front rather than emitted as broken code.
    /// </summary>
    private static void ValidateSubWorkflowCriteria(
        string stepId,
        IReadOnlyList<StepCriterion> successCriteria,
        IReadOnlyList<StepActionInfo> onSuccess,
        IReadOnlyList<StepActionInfo> onFailure)
    {
        static void Check(string stepId, IReadOnlyList<StepCriterion> criteria)
        {
            ReadOnlySpan<string> forbidden = ["$statusCode", "$response", "$request", "$method", "$url"];
            foreach (StepCriterion criterion in criteria)
            {
                string text = criterion.Context is { } context ? $"{criterion.Condition} {context}" : criterion.Condition;
                foreach (string token in forbidden)
                {
                    if (text.Contains(token, StringComparison.Ordinal))
                    {
                        throw new NotSupportedException(
                            $"Sub-workflow step '{stepId}' has a criterion referencing '{token}'; a sub-workflow step's criteria may reference only $inputs and $steps.");
                    }
                }
            }
        }

        Check(stepId, successCriteria);
        foreach (StepActionInfo action in onSuccess)
        {
            Check(stepId, action.Criteria);
        }

        foreach (StepActionInfo action in onFailure)
        {
            Check(stepId, action.Criteria);
        }
    }

    private static List<OutputMapping> ReadOutputs(in ArazzoDocument.StepObject step)
    {
        var outputs = new List<OutputMapping>();
        if (step.Outputs.IsNotUndefined())
        {
            foreach (JsonProperty<JsonElement> property in step.Outputs.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    outputs.Add(new OutputMapping(property.Name, property.Value.GetString()!));
                }
            }
        }

        return outputs;
    }

    private static string ResolveCriterionType(JsonElement type)
    {
        if (type.ValueKind == JsonValueKind.String)
        {
            return type.GetString()!;
        }

        if (type.ValueKind == JsonValueKind.Object
            && type.TryGetProperty("type"u8, out JsonElement inner)
            && inner.ValueKind == JsonValueKind.String)
        {
            return inner.GetString()!;
        }

        return "simple";
    }

    internal static void AppendWorkflowOutputs(
        StringBuilder fields,
        StringBuilder body,
        in ArazzoDocument.WorkflowObject workflow,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors)
    {
        var names = new List<string>();
        var expressions = new List<string>();
        if (workflow.Outputs.IsNotUndefined())
        {
            foreach (JsonProperty<JsonElement> property in workflow.Outputs.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    names.Add(property.Name);
                    expressions.Add(property.Value.GetString()!);
                }
            }
        }

        var statements = new StringBuilder();
        var valueLocals = new List<string>(names.Count);
        for (int i = 0; i < names.Count; i++)
        {
            string local = $"workflowOutput{i.ToString(CultureInfo.InvariantCulture)}";
            string field = $"Workflow_Output_{EmitText.SanitizeIdentifier(names[i])}";
            ValueResolution.Emit(fields, statements, expressions[i], local, "context", stepOutputLocals, field, "inputs", inputAccessors);
            valueLocals.Add(local);
        }

        statements.Append("Span<JsonElement> workflowOutputValues = [").Append(string.Join(", ", valueLocals)).AppendLine("];");
        statements.AppendLine("var workflowOutputs = JsonElement.CreateBuilder(");
        statements.AppendLine("    workspace,");
        statements.AppendLine("    (ReadOnlySpan<JsonElement>)workflowOutputValues,");
        statements.AppendLine("    static (in ReadOnlySpan<JsonElement> values, ref JsonElement.ObjectBuilder builder) =>");
        statements.AppendLine("    {");
        for (int i = 0; i < names.Count; i++)
        {
            statements.Append("        builder.AddProperty(").Append(EmitText.Quote(names[i]))
                .Append("u8, values[").Append(i.ToString(CultureInfo.InvariantCulture)).AppendLine("]);");
        }

        statements.AppendLine("    });");
        statements.AppendLine("JsonElement workflowOutputsElement = workflowOutputs.RootElement;");

        AppendIndented(body, statements.ToString(), 12);
    }

    internal static void AppendIndented(StringBuilder target, string text, int indent)
    {
        if (text.Length == 0)
        {
            return;
        }

        string pad = new(' ', indent);
        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            if (trimmed.Length == 0)
            {
                target.AppendLine();
            }
            else
            {
                target.Append(pad).AppendLine(trimmed);
            }
        }
    }

    private static string Compose(in WorkflowExecutorOptions options, string workflowId, string fields, string body, string auxiliaryTypes, bool needsContext)
    {
        var writer = new StringBuilder();
        writer.AppendLine("// <auto-generated>");
        writer.AppendLine("// This code was generated by the Corvus.Text.Json Arazzo workflow generator.");
        writer.AppendLine("// Do not edit this file directly.");
        writer.AppendLine("// </auto-generated>");
        writer.AppendLine();
        writer.AppendLine("#nullable enable");
        writer.AppendLine();
        writer.AppendLine("using System;");
        writer.AppendLine("using System.Buffers;");
        writer.AppendLine("using System.Diagnostics;");
        writer.AppendLine("using System.Text.RegularExpressions;");
        writer.AppendLine("using System.Threading;");
        writer.AppendLine("using System.Threading.Tasks;");
        writer.AppendLine("using Corvus.Text.Json;");
        writer.AppendLine("using Corvus.Text.Json.Arazzo;");
        writer.AppendLine("using Corvus.Text.Json.JsonPath;");
        writer.AppendLine("using Corvus.Text.Json.OpenApi;");
        writer.AppendLine();
        writer.Append("namespace ").Append(options.Namespace).AppendLine(";");
        writer.AppendLine();
        writer.Append("/// <summary>Generated executor for the '").Append(workflowId).AppendLine("' workflow.</summary>");

        // The class is partial so [GeneratedRegex] partial methods (emitted for regex criteria) can be
        // completed by the regular-expression source generator at the consumer's compile.
        writer.Append("public static partial class ").AppendLine(options.ClassName);
        writer.AppendLine("{");
        AppendIndented(writer, fields, 4);
        if (fields.Length > 0)
        {
            writer.AppendLine();
        }

        writer.Append("    /// <summary>Executes the '").Append(workflowId).AppendLine("' workflow.</summary>");
        writer.Append("    public static async ValueTask<").Append(options.OutputsTypeName)
            .Append("> ExecuteAsync(IApiTransport transport, JsonWorkspace workspace, ")
            .Append(options.InputsTypeName).AppendLine(" inputs, CancellationToken cancellationToken = default)");
        writer.AppendLine("    {");
        writer.AppendLine("        ArgumentNullException.ThrowIfNull(transport);");
        writer.AppendLine("        ArgumentNullException.ThrowIfNull(workspace);");

        // The context is created only when a criterion or value still resolves through it.
        if (needsContext)
        {
            writer.AppendLine("        var context = new WorkflowExecutionContext();");
            writer.AppendLine("        context.SetInputs(inputs);");
        }

        writer.Append("        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity(\"workflow.").Append(workflowId).AppendLine("\");");
        writer.AppendLine("        ArazzoTelemetry.WorkflowsStarted.Add(1);");
        writer.AppendLine("        try");
        writer.AppendLine("        {");
        writer.Append(body);
        writer.AppendLine("            ArazzoTelemetry.WorkflowsCompleted.Add(1);");
        writer.AppendLine("            return workflowOutputsElement;");
        writer.AppendLine("        }");
        writer.AppendLine("        catch (Exception ex)");
        writer.AppendLine("        {");
        writer.AppendLine("            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);");
        writer.AppendLine("            ArazzoTelemetry.WorkflowsFaulted.Add(1);");
        writer.AppendLine("            throw;");
        writer.AppendLine("        }");
        writer.AppendLine("    }");
        writer.AppendLine("}");

        // Sibling types (ahead-of-time-compiled jsonpath query classes) live after the executor class
        // in the same namespace.
        if (auxiliaryTypes.Length > 0)
        {
            writer.AppendLine();
            writer.Append(auxiliaryTypes);
        }

        return writer.ToString();
    }
}

/// <summary>
/// Options controlling executor emission (plan §3.1).
/// </summary>
/// <param name="Namespace">The .NET namespace for the generated executor.</param>
/// <param name="ClassName">The generated executor class name (e.g. <c>AdoptWorkflow</c>).</param>
/// <param name="InputsTypeName">The fully-qualified type of the workflow inputs.</param>
/// <param name="OutputsTypeName">The fully-qualified type of the workflow outputs.</param>
/// <param name="InputAccessors">
/// Map of input JSON name → generated dotnet accessor property on the inputs model (e.g.
/// <c>petId</c> → <c>PetId</c>), so <c>$inputs.&lt;name&gt;</c> compiles to a strongly-typed accessor.
/// <see langword="null"/> when the inputs are an untyped <see cref="JsonElement"/>.
/// </param>
public readonly record struct WorkflowExecutorOptions(
    string Namespace,
    string ClassName,
    string InputsTypeName,
    string OutputsTypeName,
    IReadOnlyDictionary<string, string>? InputAccessors = null);

/// <summary>The control-flow effect of an Arazzo success/failure action.</summary>
internal enum StepActionKind
{
    /// <summary>End the workflow (jump to building the outputs).</summary>
    End,

    /// <summary>Transfer control to another step (sub-workflow targets are a later phase).</summary>
    Goto,

    /// <summary>Retry the current step (failure actions only), up to <c>retryLimit</c> times.</summary>
    Retry,
}

/// <summary>
/// A success/failure action read off a step (plan §3.3): a control-flow effect gated by criteria.
/// </summary>
/// <param name="Name">The action name.</param>
/// <param name="Kind">The effect (end/goto/retry).</param>
/// <param name="TargetStepId">The goto target step id, if any.</param>
/// <param name="TargetWorkflowId">The goto/retry target workflow id, if any (sub-workflow — later phase).</param>
/// <param name="RetryAfter">The retry delay in seconds, if specified.</param>
/// <param name="RetryLimit">The retry limit, if specified (defaults to a single retry).</param>
/// <param name="Criteria">The criteria gating this action (an empty set always matches).</param>
internal readonly record struct StepActionInfo(
    string Name,
    StepActionKind Kind,
    string? TargetStepId,
    string? TargetWorkflowId,
    double? RetryAfter,
    int? RetryLimit,
    IReadOnlyList<StepCriterion> Criteria);

/// <summary>
/// A fully-bound workflow step (its resolved operation plus everything the emitter reads off the typed
/// document), shared between the straight-line and control-flow emission paths.
/// </summary>
internal readonly record struct ControlFlowStep(
    string StepId,
    ResolvedOperation? Operation,
    IReadOnlyList<StepArgument> Arguments,
    IReadOnlyList<StepCriterion> SuccessCriteria,
    IReadOnlyList<OutputMapping> Outputs,
    StepBody? RequestBody,
    bool BindResponseBody,
    IReadOnlyList<StepActionInfo> OnSuccess,
    IReadOnlyList<StepActionInfo> OnFailure,
    string? SubWorkflowId = null);