// <copyright file="OutputExtractionEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits the code that projects a step's named outputs into a built JSON object held in a local
/// (plan §3.1).
/// </summary>
/// <remarks>
/// Each output value is resolved in method scope (so <c>$steps.&lt;id&gt;.outputs</c> references resolve
/// statically with no dictionary) and the object is then built with the mutable-document builder
/// using a closure-free, context-carrying delegate whose context is the
/// <see cref="ReadOnlySpan{T}"/> of pre-resolved values — so any number of outputs are added directly,
/// with no intermediate buffers and no per-run dictionary. The built document is owned by the
/// caller-provided <c>JsonWorkspace</c>, whose lifetime spans the run; the step's outputs object is
/// kept in the <see cref="EmitText.StepOutputsElementLocal(string)"/> local for later
/// <c>$steps.&lt;id&gt;.outputs</c> references.
/// </remarks>
public static class OutputExtractionEmitter
{
    /// <summary>
    /// Emits the field declarations and in-method statements that build a step's outputs object.
    /// </summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="outputs">The step's outputs (name → runtime expression), in document order.</param>
    /// <param name="workspaceVariable">The in-scope <c>JsonWorkspace</c> variable name.</param>
    /// <param name="contextVariable">The in-scope <c>WorkflowExecutionContext</c> variable name.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputsVariable">The in-scope workflow inputs variable name (for static <c>$inputs</c> navigation).</param>
    /// <param name="inputAccessors">Map of input JSON name → generated dotnet accessor on the inputs model, or <see langword="null"/> for untyped inputs.</param>
    /// <param name="responseBodyLocal">
    /// The in-scope local holding the live matched-status response body (for <c>$response.body</c>
    /// projection), or <see langword="null"/> when the step references no response body. The projected
    /// value is the only thing copied (via <c>CloneAsBuilder</c>) — it outlives the response, which is
    /// disposed once this build completes.
    /// </param>
    /// <returns>The emitted static field declarations and the in-method statements (empty when there are no outputs). The statements ASSIGN the pre-declared step-outputs element local.</returns>
    public static OutputExtractionCode Emit(
        string stepId,
        IReadOnlyList<OutputMapping> outputs,
        string workspaceVariable,
        string contextVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors,
        string? responseBodyLocal)
    {
        ArgumentException.ThrowIfNullOrEmpty(stepId);
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(stepOutputLocals);

        if (outputs.Count == 0)
        {
            return new OutputExtractionCode(string.Empty, string.Empty);
        }

        string identifier = EmitText.SanitizeIdentifier(stepId);
        string builderVariable = $"{EmitText.ToCamelCase(identifier)}Outputs";
        string elementVariable = EmitText.StepOutputsElementLocal(stepId);

        var fields = new StringBuilder();
        var statements = new StringBuilder();
        var valueLocals = new List<string>(outputs.Count);

        for (int i = 0; i < outputs.Count; i++)
        {
            string local = $"{EmitText.ToCamelCase(identifier)}Output{i.ToString(CultureInfo.InvariantCulture)}";
            string field = $"{identifier}_Output_{EmitText.SanitizeIdentifier(outputs[i].Name)}";
            ArazzoExpression parsed = ArazzoExpression.Parse(outputs[i].Expression);

            if (parsed.Source == ArazzoExpressionSource.ResponseBody && responseBodyLocal is not null)
            {
                // Project from the live response body, copying ONLY this value into the workspace so it
                // outlives the response (the only reification — and it's the output product itself).
                EmitResponseBodyProjection(statements, responseBodyLocal, parsed.JsonPointer, workspaceVariable, local);
            }
            else
            {
                // $inputs / $steps.*.outputs resolve to references into documents that outlive the
                // outputs object; other sources still flow through the context for now.
                ValueResolution.Emit(fields, statements, outputs[i].Expression, local, contextVariable, stepOutputLocals, field, inputsVariable, inputAccessors);
            }

            valueLocals.Add(local);
        }

        statements.Append("Span<JsonElement> ").Append(builderVariable).Append("Values = [")
            .Append(string.Join(", ", valueLocals)).AppendLine("];");
        statements.Append("var ").Append(builderVariable).AppendLine(" = JsonElement.CreateBuilder(");
        statements.Append("    ").Append(workspaceVariable).AppendLine(",");
        statements.Append("    (ReadOnlySpan<JsonElement>)").Append(builderVariable).AppendLine("Values,");
        statements.AppendLine("    static (in ReadOnlySpan<JsonElement> values, ref JsonElement.ObjectBuilder builder) =>");
        statements.AppendLine("    {");
        for (int i = 0; i < outputs.Count; i++)
        {
            statements.Append("        builder.AddProperty(").Append(EmitText.Quote(outputs[i].Name))
                .Append("u8, values[").Append(i.ToString(CultureInfo.InvariantCulture)).AppendLine("]);");
        }

        statements.AppendLine("    });");

        // Assign the pre-declared element local (declared before the step's try so later steps can
        // reference it; built here, inside the try, while the response is alive).
        statements.Append(elementVariable).Append(" = ").Append(builderVariable).AppendLine(".RootElement;");

        return new OutputExtractionCode(fields.ToString(), statements.ToString());
    }

    private static void EmitResponseBodyProjection(StringBuilder statements, string responseBodyLocal, string? jsonPointer, string workspaceVariable, string resultLocal)
    {
        if (string.IsNullOrEmpty(jsonPointer))
        {
            statements.Append("JsonElement ").Append(resultLocal).Append(" = ").Append(responseBodyLocal)
                .Append(".CloneAsBuilder(").Append(workspaceVariable).AppendLine(").RootElement;");
        }
        else
        {
            statements.Append("JsonElement ").Append(resultLocal).AppendLine(" = default;");
            statements.Append("if (").Append(responseBodyLocal).Append(".TryResolvePointer(")
                .Append(EmitText.Quote(jsonPointer)).Append("u8, out JsonElement ").Append(resultLocal).AppendLine("Nav))");
            statements.AppendLine("{");
            statements.Append("    ").Append(resultLocal).Append(" = ").Append(resultLocal)
                .Append("Nav.CloneAsBuilder(").Append(workspaceVariable).AppendLine(").RootElement;");
            statements.AppendLine("}");
        }
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
/// <param name="Statements">The in-method statements that build the step's outputs object.</param>
public readonly record struct OutputExtractionCode(string Fields, string Statements);