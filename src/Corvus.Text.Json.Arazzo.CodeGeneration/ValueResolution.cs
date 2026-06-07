// <copyright file="ValueResolution.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits the code that resolves a runtime expression to a <see cref="JsonElement"/> local (plan §3.1).
/// </summary>
/// <remarks>
/// A <c>$steps.&lt;id&gt;.outputs.&lt;name&gt;</c> reference is resolved <em>statically</em> — the step's
/// id is known at generation time, so it compiles to direct navigation of that step's outputs local,
/// with no runtime dictionary lookup and no allocation. Every other source (the current step's
/// <c>$inputs</c>, <c>$response</c>, <c>$statusCode</c>, …) flows through the
/// <see cref="WorkflowExecutionContext"/>'s field-based, allocation-free resolution.
/// </remarks>
internal static class ValueResolution
{
    /// <summary>
    /// Emits the static field declarations and method-scope statements that assign a
    /// <see cref="JsonElement"/> named <paramref name="resultLocal"/> from a runtime expression.
    /// </summary>
    /// <param name="fields">Accumulates <c>static readonly</c> field declarations.</param>
    /// <param name="statements">Accumulates the in-method resolution statements.</param>
    /// <param name="expression">The runtime expression.</param>
    /// <param name="resultLocal">The name of the <see cref="JsonElement"/> local to assign.</param>
    /// <param name="contextVariable">The in-scope <see cref="WorkflowExecutionContext"/> variable name.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="fieldName">The unique name for the compiled-expression field (used for the context path).</param>
    public static void Emit(
        StringBuilder fields,
        StringBuilder statements,
        string expression,
        string resultLocal,
        string contextVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string fieldName)
    {
        ArazzoExpression parsed = ArazzoExpression.Parse(expression);

        if (parsed.Source == ArazzoExpressionSource.Steps
            && parsed.ContainerId is { } stepId
            && parsed.Name is { } outputName
            && stepOutputLocals.TryGetValue(stepId, out string? stepLocal))
        {
            // Static: navigate the step's outputs object directly — no context, no dictionary.
            if (string.IsNullOrEmpty(parsed.JsonPointer))
            {
                statements.Append(stepLocal).Append(".TryGetProperty(")
                    .Append(EmitText.Quote(outputName)).Append("u8, out JsonElement ").Append(resultLocal).AppendLine(");");
            }
            else
            {
                statements.Append("JsonElement ").Append(resultLocal).AppendLine(" = default;");
                statements.Append("if (").Append(stepLocal).Append(".TryGetProperty(")
                    .Append(EmitText.Quote(outputName)).Append("u8, out JsonElement ").Append(resultLocal).AppendLine("Property))");
                statements.AppendLine("{");
                statements.Append("    ").Append(resultLocal).Append("Property.TryResolvePointer(")
                    .Append(EmitText.Quote(parsed.JsonPointer)).Append("u8, out ").Append(resultLocal).AppendLine(");");
                statements.AppendLine("}");
            }

            return;
        }

        // Current-step / other sources: the context resolves these from fields (no dictionary).
        fields.Append("private static readonly ArazzoExpression ").Append(fieldName)
            .Append(" = ArazzoExpression.Parse(").Append(EmitText.Quote(expression)).AppendLine(");");
        statements.Append(contextVariable).Append(".TryResolveValue(")
            .Append(fieldName).Append(", out JsonElement ").Append(resultLocal).AppendLine(");");
    }
}