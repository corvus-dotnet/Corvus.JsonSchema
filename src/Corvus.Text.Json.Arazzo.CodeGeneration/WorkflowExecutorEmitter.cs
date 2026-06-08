// <copyright file="WorkflowExecutorEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo10;

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
    /// <returns>The C# source of the generated executor class.</returns>
    public static string Emit(
        in ArazzoDocument.WorkflowObject workflow,
        WorkflowOperationBinder binder,
        in WorkflowExecutorOptions options)
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
        foreach (ArazzoDocument.StepObject step in workflow.Steps.EnumerateArray())
        {
            string stepId = step.StepId.IsNotUndefined() ? step.StepId.GetString()! : throw new InvalidOperationException("A step is missing its required stepId.");
            StepBinding binding = binder.Bind(step);

            if (binding.Kind is not (StepTargetKind.OperationId or StepTargetKind.OperationPath) || binding.Operation is not { } operation)
            {
                throw new InvalidOperationException(
                    $"Step '{stepId}' targets {binding.Kind}; only operation steps are supported by the current generator.");
            }

            List<OutputMapping> stepOutputs = ReadOutputs(step);
            List<StepCriterion> criteria = ReadCriteria(step);
            List<StepActionInfo> onSuccess = ReadActions(step.OnSuccess);
            List<StepActionInfo> onFailure = ReadActions(step.OnFailure);
            usesControlFlow |= onSuccess.Count > 0 || onFailure.Count > 0;

            // Only clone the response body into the workspace when the step actually consumes it
            // (success criteria, outputs, or an action's criteria).
            bool bindResponseBody = ReferencesResponseBody(criteria, stepOutputs, onSuccess, onFailure);

            boundSteps.Add(new ControlFlowStep(
                stepId, operation, ReadArguments(step), criteria, stepOutputs, ReadRequestBody(step), bindResponseBody, onSuccess, onFailure));
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

                // The step body builds the step's outputs product inside the step (while the response
                // is alive), so output extraction is not a separate post-step pass.
                StepBodyCode stepBody = StepBodyEmitter.Emit(
                    step.StepId, step.Operation, step.Arguments, step.SuccessCriteria, step.Outputs, "transport", "workspace", "context", "cancellationToken", stepOutputLocals, "inputs", options.InputAccessors, options.Namespace, step.RequestBody, step.BindResponseBody);
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

    private static List<StepArgument> ReadArguments(in ArazzoDocument.StepObject step)
    {
        var arguments = new List<StepArgument>();
        if (step.Parameters.IsNotUndefined())
        {
            foreach (JsonElement element in step.Parameters.EnumerateArray())
            {
                ArazzoDocument.ParameterObject parameter = element;
                if (!parameter.Name.IsNotUndefined())
                {
                    // A reusable-parameter reference ({reference:…}); component resolution is a later phase.
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
    /// Reads a step's <c>onSuccess</c>/<c>onFailure</c> array into a list of <see cref="StepActionInfo"/>.
    /// Inline action objects are read; a reusable-action reference (<c>{reference:…}</c>) is skipped —
    /// component resolution is a later phase.
    /// </summary>
    private static List<StepActionInfo> ReadActions(in JsonElement actions)
    {
        var list = new List<StepActionInfo>();
        if (actions.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (JsonElement entity in actions.EnumerateArray())
        {
            if (entity.ValueKind != JsonValueKind.Object || entity.TryGetProperty("reference"u8, out _))
            {
                continue;
            }

            if (!entity.TryGetProperty("name"u8, out JsonElement nameElement) || nameElement.ValueKind != JsonValueKind.String
                || !entity.TryGetProperty("type"u8, out JsonElement typeElement) || typeElement.ValueKind != JsonValueKind.String)
            {
                continue;
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

            list.Add(new StepActionInfo(nameElement.GetString()!, kind, targetStepId, targetWorkflowId, retryAfter, retryLimit, criteria));
        }

        return list;
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
    ResolvedOperation Operation,
    IReadOnlyList<StepArgument> Arguments,
    IReadOnlyList<StepCriterion> SuccessCriteria,
    IReadOnlyList<OutputMapping> Outputs,
    StepBody? RequestBody,
    bool BindResponseBody,
    IReadOnlyList<StepActionInfo> OnSuccess,
    IReadOnlyList<StepActionInfo> OnFailure);