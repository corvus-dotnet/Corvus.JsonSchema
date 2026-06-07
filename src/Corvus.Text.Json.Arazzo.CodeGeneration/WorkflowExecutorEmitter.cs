// <copyright file="WorkflowExecutorEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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

        foreach (ArazzoDocument.StepObject step in workflow.Steps.EnumerateArray())
        {
            string stepId = step.StepId.IsNotUndefined() ? step.StepId.GetString()! : throw new InvalidOperationException("A step is missing its required stepId.");
            StepBinding binding = binder.Bind(step);

            if (binding.Kind is not (StepTargetKind.OperationId or StepTargetKind.OperationPath) || binding.Operation is not { } operation)
            {
                throw new InvalidOperationException(
                    $"Step '{stepId}' targets {binding.Kind}; only operation steps are supported by the current generator.");
            }

            body.Append("            // ── step: ").Append(stepId).AppendLine(" ──");

            StepBodyCode stepBody = StepBodyEmitter.Emit(
                stepId, operation, ReadArguments(step), ReadCriteria(step), "transport", "context", "cancellationToken");
            fields.Append(stepBody.Fields);
            AppendIndented(body, stepBody.Statements, 12);

            OutputExtractionCode outputs = OutputExtractionEmitter.Emit(stepId, ReadOutputs(step), "workspace", "context");
            fields.Append(outputs.Fields);
            AppendIndented(body, outputs.Statements, 12);

            body.AppendLine();
        }

        AppendWorkflowOutputs(fields, body, workflow);

        return Compose(options, workflowId, fields.ToString(), body.ToString());
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

                JsonElement value = (JsonElement)parameter.Value;
                if (value.ValueKind == JsonValueKind.String)
                {
                    arguments.Add(new StepArgument(parameter.Name.GetString()!, value.GetString()!));
                }

                // Non-string (literal) parameter values are bound in a later stage.
            }
        }

        return arguments;
    }

    private static List<StepCriterion> ReadCriteria(in ArazzoDocument.StepObject step)
    {
        var criteria = new List<StepCriterion>();
        if (step.SuccessCriteria.IsNotUndefined())
        {
            foreach (ArazzoDocument.CriterionObject criterion in step.SuccessCriteria.EnumerateArray())
            {
                string condition = criterion.Condition.GetString()!;
                string? context = criterion.Context.IsNotUndefined() ? criterion.Context.GetString() : null;
                criteria.Add(new StepCriterion(ResolveCriterionType(criterion.Type), condition, context));
            }
        }

        return criteria;
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

    private static void AppendWorkflowOutputs(StringBuilder fields, StringBuilder body, in ArazzoDocument.WorkflowObject workflow)
    {
        var build = new StringBuilder();
        if (workflow.Outputs.IsNotUndefined())
        {
            foreach (JsonProperty<JsonElement> property in workflow.Outputs.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string field = $"Workflow_Output_{EmitText.SanitizeIdentifier(property.Name)}";
                string local = $"{EmitText.ToCamelCase(EmitText.SanitizeIdentifier(property.Name))}Value";
                fields.Append("private static readonly ArazzoExpression ").Append(field)
                    .Append(" = ArazzoExpression.Parse(").Append(EmitText.Quote(property.Value.GetString()!)).AppendLine(");");

                build.Append("                ctx.TryResolveValue(").Append(field).Append(", out JsonElement ").Append(local).AppendLine(");");
                build.Append("                builder.AddProperty(").Append(EmitText.Quote(property.Name)).Append("u8, ").Append(local).AppendLine(");");
            }
        }

        body.AppendLine("            var workflowOutputs = JsonElement.CreateBuilder(");
        body.AppendLine("                workspace,");
        body.AppendLine("                new JsonElement.Source(context,");
        body.AppendLine("                    static (in WorkflowExecutionContext ctx, ref JsonElement.ObjectBuilder builder) =>");
        body.AppendLine("                    {");
        body.Append(build);
        body.AppendLine("                    }));");
        body.AppendLine("            JsonElement workflowOutputsElement = workflowOutputs.RootElement;");
    }

    private static void AppendIndented(StringBuilder target, string text, int indent)
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

    private static string Compose(in WorkflowExecutorOptions options, string workflowId, string fields, string body)
    {
        var writer = new StringBuilder();
        writer.AppendLine("// <auto-generated>");
        writer.AppendLine("// This code was generated by the Corvus.Text.Json Arazzo workflow generator.");
        writer.AppendLine("// Do not edit this file directly.");
        writer.AppendLine("// </auto-generated>");
        writer.AppendLine();
        writer.AppendLine("#nullable enable");
        writer.AppendLine();
        writer.AppendLine("using System.Diagnostics;");
        writer.AppendLine("using System.Threading;");
        writer.AppendLine("using System.Threading.Tasks;");
        writer.AppendLine("using Corvus.Text.Json;");
        writer.AppendLine("using Corvus.Text.Json.Arazzo;");
        writer.AppendLine("using Corvus.Text.Json.OpenApi;");
        writer.AppendLine();
        writer.Append("namespace ").Append(options.Namespace).AppendLine(";");
        writer.AppendLine();
        writer.Append("/// <summary>Generated executor for the '").Append(workflowId).AppendLine("' workflow.</summary>");
        writer.Append("public static class ").AppendLine(options.ClassName);
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
        writer.AppendLine("        var context = new WorkflowExecutionContext();");
        writer.AppendLine("        context.SetInputs(inputs);");
        writer.Append("        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity(\"workflow.").Append(workflowId).AppendLine("\");");
        writer.AppendLine("        ArazzoTelemetry.WorkflowsStarted.Add(1);");
        writer.AppendLine("        try");
        writer.AppendLine("        {");
        writer.Append(body);
        writer.AppendLine("            ArazzoTelemetry.WorkflowsCompleted.Add(1);");
        writer.AppendLine("            return workflowOutputsElement;");
        writer.AppendLine("        }");
        writer.AppendLine("        catch");
        writer.AppendLine("        {");
        writer.AppendLine("            ArazzoTelemetry.WorkflowsFaulted.Add(1);");
        writer.AppendLine("            throw;");
        writer.AppendLine("        }");
        writer.AppendLine("    }");
        writer.AppendLine("}");
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
public readonly record struct WorkflowExecutorOptions(
    string Namespace,
    string ClassName,
    string InputsTypeName,
    string OutputsTypeName);