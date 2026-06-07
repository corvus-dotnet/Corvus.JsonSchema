// <copyright file="OutputExtractionEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits the code that projects a step's named outputs into the workflow run state (plan §3.1).
/// </summary>
/// <remarks>
/// Each output expression is parsed once into a <c>static readonly</c> <c>ArazzoExpression</c> field.
/// At run time the outputs object is built with the mutable-document builder using a closure-free,
/// context-carrying <c>ObjectBuilder.Build&lt;TContext&gt;</c> delegate — each value resolved to a
/// <see cref="JsonElement"/> and added directly, with no intermediate buffers — then registered with
/// the workflow context. The built document is owned by the caller-provided <c>JsonWorkspace</c>,
/// whose lifetime spans the whole run (later steps read <c>$steps.&lt;id&gt;.outputs</c>).
/// </remarks>
public static class OutputExtractionEmitter
{
    /// <summary>
    /// Emits the field declarations and in-method statements that extract a step's outputs.
    /// </summary>
    /// <param name="stepId">The step id (the key the outputs are registered under).</param>
    /// <param name="outputs">The step's outputs (name → runtime expression), in document order.</param>
    /// <param name="workspaceVariable">The in-scope <c>JsonWorkspace</c> variable name.</param>
    /// <param name="contextVariable">The in-scope <c>WorkflowExecutionContext</c> variable name.</param>
    /// <returns>The emitted static field declarations and the in-method statements (empty when there are no outputs).</returns>
    public static OutputExtractionCode Emit(
        string stepId,
        IReadOnlyList<OutputMapping> outputs,
        string workspaceVariable,
        string contextVariable)
    {
        ArgumentException.ThrowIfNullOrEmpty(stepId);
        ArgumentNullException.ThrowIfNull(outputs);

        if (outputs.Count == 0)
        {
            return new OutputExtractionCode(string.Empty, string.Empty);
        }

        string identifier = EmitText.SanitizeIdentifier(stepId);
        string outputsVariable = $"{EmitText.ToCamelCase(identifier)}Outputs";

        var fields = new StringBuilder();
        var build = new StringBuilder();

        foreach (OutputMapping output in outputs)
        {
            string field = $"{identifier}_Output_{EmitText.SanitizeIdentifier(output.Name)}";
            string local = $"{EmitText.ToCamelCase(EmitText.SanitizeIdentifier(output.Name))}Value";

            fields.Append("private static readonly ArazzoExpression ").Append(field)
                .Append(" = ArazzoExpression.Parse(").Append(EmitText.Quote(output.Expression)).AppendLine(");");

            build.Append("        ctx.TryResolveValue(").Append(field).Append(", out JsonElement ").Append(local).AppendLine(");");
            build.Append("        builder.AddProperty(").Append(EmitText.Quote(output.Name)).Append("u8, ").Append(local).AppendLine(");");
        }

        var statements = new StringBuilder();
        statements.Append("var ").Append(outputsVariable).AppendLine(" = JsonElement.CreateBuilder(");
        statements.Append("    ").Append(workspaceVariable).AppendLine(",");
        statements.Append("    new JsonElement.Source(").Append(contextVariable).AppendLine(",");
        statements.AppendLine("        static (in WorkflowExecutionContext ctx, ref JsonElement.ObjectBuilder builder) =>");
        statements.AppendLine("        {");
        statements.Append(build);
        statements.AppendLine("        }));");
        statements.Append(contextVariable).Append(".SetStepOutputs(").Append(EmitText.Quote(stepId)).Append(", ").Append(outputsVariable).AppendLine(".RootElement);");

        return new OutputExtractionCode(fields.ToString(), statements.ToString());
    }
}

/// <summary>
/// A named output a step projects (plan §3.1).
/// </summary>
/// <param name="Name">The output name.</param>
/// <param name="Expression">The runtime expression that produces the value.</param>
public readonly record struct OutputMapping(string Name, string Expression);

/// <summary>
/// The code emitted for a step's output extraction (plan §3.1).
/// </summary>
/// <param name="Fields">The <c>static readonly</c> field declarations to place on the executor class.</param>
/// <param name="Statements">The in-method statements that build and register the step's outputs.</param>
public readonly record struct OutputExtractionCode(string Fields, string Statements);