// <copyright file="SubWorkflowStepEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits a sub-workflow step (plan §3.3): a step whose target is another workflow's generated executor.
/// The step's parameters are projected into an inputs object that is handed to the target's
/// <c>ExecuteAsync</c> (a <see cref="JsonElement"/> converts to the target's inputs type via its
/// implicit operator), and the workflow it returns becomes this step's outputs object — so
/// <c>$steps.&lt;id&gt;.outputs</c> resolves against the sub-workflow's outputs.
/// </summary>
/// <remarks>
/// The target executor is generated in the same workflows namespace as the caller, so its
/// fully-qualified name is computed directly from the workflow id (<c>{ns}.{Pascal}Workflow</c>) — no
/// cross-workflow type map is needed. The caller-owned <c>JsonWorkspace</c> flows through, so the
/// sub-workflow's products are owned by the same run. Reusable-parameter references and non-expression
/// parameter values are later phases.
/// </remarks>
internal static class SubWorkflowStepEmitter
{
    /// <summary>
    /// Emits the fields and statements that invoke a sub-workflow and capture its outputs.
    /// </summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="subWorkflowId">The target workflow id.</param>
    /// <param name="arguments">The step's parameters (each becomes a named input of the sub-workflow).</param>
    /// <param name="workflowsNamespace">The namespace the generated executors share.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputsVariable">The caller's inputs variable name (for <c>$inputs</c> navigation in parameters).</param>
    /// <param name="inputAccessors">The caller's input accessor map, or <see langword="null"/> for untyped inputs.</param>
    /// <returns>The emitted fields and statements.</returns>
    public static SubWorkflowStepCode Emit(
        string stepId,
        string subWorkflowId,
        IReadOnlyList<StepArgument> arguments,
        string workflowsNamespace,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors)
    {
        ArgumentException.ThrowIfNullOrEmpty(stepId);
        ArgumentException.ThrowIfNullOrEmpty(subWorkflowId);
        ArgumentNullException.ThrowIfNull(arguments);

        string identifier = EmitText.SanitizeIdentifier(stepId);
        string prefix = $"{identifier}_";
        string camel = EmitText.ToCamelCase(identifier);
        string builderVariable = $"{camel}Inputs";
        string outputsLocal = EmitText.StepOutputsElementLocal(stepId);
        string targetClass = $"{workflowsNamespace}.{EmitText.ToPascalCase(subWorkflowId)}Workflow";

        var fields = new StringBuilder();
        var statements = new StringBuilder();
        var valueLocals = new List<string>(arguments.Count);

        foreach (StepArgument argument in arguments)
        {
            if (argument.Kind != ArgumentValueKind.Expression)
            {
                throw new NotSupportedException(
                    $"Sub-workflow step '{stepId}' binds parameter '{argument.Name}' to a non-expression value; only runtime-expression parameters are supported on a sub-workflow step.");
            }

            string local = $"{camel}Input{valueLocals.Count.ToString(CultureInfo.InvariantCulture)}";
            string field = $"{prefix}Input_{EmitText.SanitizeIdentifier(argument.Name)}";
            ValueResolution.Emit(fields, statements, argument.Value, local, "context", stepOutputLocals, field, inputsVariable, inputAccessors);
            valueLocals.Add(local);
        }

        // Project the parameters into the sub-workflow's inputs object, then invoke it. A JsonElement
        // converts to the target's inputs type (typed model or JsonElement) via its implicit operator.
        statements.Append("Span<JsonElement> ").Append(builderVariable).Append("Values = [")
            .Append(string.Join(", ", valueLocals)).AppendLine("];");
        statements.Append("var ").Append(builderVariable).AppendLine(" = JsonElement.CreateBuilder(");
        statements.AppendLine("    workspace,");
        statements.Append("    (ReadOnlySpan<JsonElement>)").Append(builderVariable).AppendLine("Values,");
        statements.AppendLine("    static (in ReadOnlySpan<JsonElement> values, ref JsonElement.ObjectBuilder builder) =>");
        statements.AppendLine("    {");
        for (int i = 0; i < arguments.Count; i++)
        {
            statements.Append("        builder.AddProperty(").Append(EmitText.Quote(arguments[i].Name))
                .Append("u8, values[").Append(i.ToString(CultureInfo.InvariantCulture)).AppendLine("]);");
        }

        statements.AppendLine("    });");

        statements.Append("ArazzoTelemetry.StepsExecuted.Add(1);");
        statements.AppendLine();
        statements.Append("JsonElement ").Append(outputsLocal).Append(" = await ").Append(targetClass)
            .Append(".ExecuteAsync(transport, workspace, ").Append(builderVariable).AppendLine(".RootElement, cancellationToken).ConfigureAwait(false);");

        return new SubWorkflowStepCode(fields.ToString(), statements.ToString());
    }
}

/// <summary>
/// The code emitted for a sub-workflow step (plan §3.3).
/// </summary>
/// <param name="Fields">The <c>static readonly</c> field declarations to place on the executor class.</param>
/// <param name="Statements">The in-method statements that invoke the sub-workflow and capture its outputs.</param>
internal readonly record struct SubWorkflowStepCode(string Fields, string Statements);