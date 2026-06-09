// <copyright file="ReplyTemplateEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Builds a request/reply <c>receive</c> step's reply payload from a template that may mix constant JSON
/// with embedded runtime expressions (<c>$message.payload</c>, <c>$inputs</c>, <c>$steps</c>) — so a
/// responder can construct a reply object/array from request fields, or an interpolated reply string,
/// rather than only echoing a single value.
/// </summary>
/// <remarks>
/// <para>
/// The template is walked at generation time. A subtree with no embedded expression is baked once into a
/// static constant document and referenced. A subtree that embeds expressions is built into the run's
/// <see cref="JsonWorkspace"/> via the object/array builder: each property/item value is resolved first
/// (an expression to a reference into the live request/inputs/step-outputs, an interpolation to a pooled
/// build, a constant to a baked reference, a nested object/array by recursion) and then assembled. The
/// result outlives the handler because the transport serializes the reply synchronously while the
/// workspace, the live request, and the baked constants are all alive.
/// </para>
/// <para>
/// A string value beginning with <c>$</c> is treated as a runtime expression and a string containing
/// <c>{$…}</c> as an interpolation, mirroring how the request side classifies argument values; a literal
/// string that merely contains a <c>$</c> elsewhere is emitted as a constant.
/// </para>
/// </remarks>
internal static class ReplyTemplateEmitter
{
    /// <summary>
    /// Emits the statements that build a composite (object/array) reply template into the workspace and
    /// returns the C# expression yielding the resulting <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="stepId">The step id (for diagnostics).</param>
    /// <param name="rawJson">The template's raw JSON text.</param>
    /// <param name="workspaceVariable">The in-scope <c>JsonWorkspace</c> variable name.</param>
    /// <param name="sources">The criterion sources bundle (the live request payload as the body, the headers).</param>
    /// <param name="inputsVariable">The workflow inputs variable name.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputAccessors">The input accessor map, or <see langword="null"/> for untyped inputs.</param>
    /// <param name="fields">Accumulates <c>static readonly</c> field declarations (baked constants).</param>
    /// <param name="statements">Accumulates the in-method build statements.</param>
    /// <param name="baseName">A unique prefix for the emitted temporaries/fields.</param>
    /// <returns>The expression yielding the built reply <see cref="JsonElement"/>.</returns>
    public static string EmitComposite(
        string stepId,
        string rawJson,
        string workspaceVariable,
        in CriterionSources sources,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        StringBuilder fields,
        StringBuilder statements,
        string baseName)
    {
        using Stj.JsonDocument document = Stj.JsonDocument.Parse(rawJson);
        return EmitValue(stepId, document.RootElement, workspaceVariable, sources, inputsVariable, stepOutputLocals, inputAccessors, fields, statements, baseName);
    }

    /// <summary>
    /// Emits the statements that build an interpolated string reply into the workspace and returns the
    /// expression yielding the resulting <see cref="JsonElement"/>.
    /// </summary>
    public static string EmitInterpolation(
        string stepId,
        string template,
        string workspaceVariable,
        in CriterionSources sources,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        StringBuilder statements,
        string baseName)
    {
        if (!InterpolationInliner.TryEmit(
                template, sources.BodyLocal, inputsVariable, stepOutputLocals, inputAccessors, $"{baseName}I", out InterpolationInlineCode code))
        {
            throw new NotSupportedException(
                $"Request/reply receive step '{stepId}' has an interpolated reply fragment '{template}' that references a value other than $message, $inputs, or $steps.");
        }

        statements.Append(code.Statements);
        string local = $"{baseName}Istr";
        statements.Append("JsonElement ").Append(local).Append(" = JsonElement.CreateBuilder(")
            .Append(workspaceVariable).Append(", ").Append(code.SourceExpression).AppendLine(").RootElement;");
        statements.Append(code.Cleanup);
        return local;
    }

    /// <summary>
    /// Bakes a constant JSON value into a static document field and returns its root element expression.
    /// </summary>
    public static string EmitConstant(string json, StringBuilder fields, string fieldName)
    {
        fields.Append("private static readonly ParsedJsonDocument<JsonElement> ").Append(fieldName)
            .Append(" = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(")
            .Append(EmitText.Quote(json)).AppendLine("));");
        return $"{fieldName}.RootElement";
    }

    // Resolves one template node to a C# expression yielding its JsonElement value, emitting any build
    // statements/fields needed first.
    private static string EmitValue(
        string stepId,
        in Stj.JsonElement template,
        string workspaceVariable,
        in CriterionSources sources,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        StringBuilder fields,
        StringBuilder statements,
        string baseName)
    {
        // A subtree with no embedded expression is a constant — bake it once and reference it.
        string raw = template.GetRawText();
        if (!raw.Contains('$', StringComparison.Ordinal))
        {
            return EmitConstant(raw, fields, $"{baseName}Const");
        }

        switch (template.ValueKind)
        {
            case Stj.JsonValueKind.Object:
            {
                var locals = new List<string>();
                var names = new List<string>();
                int index = 0;
                foreach (Stj.JsonProperty property in template.EnumerateObject())
                {
                    locals.Add(EmitValue(stepId, property.Value, workspaceVariable, sources, inputsVariable, stepOutputLocals, inputAccessors, fields, statements, $"{baseName}_{index.ToString(CultureInfo.InvariantCulture)}"));
                    names.Add(property.Name);
                    index++;
                }

                string builder = $"{baseName}Obj";
                statements.Append("Span<JsonElement> ").Append(builder).Append("Values = [").Append(string.Join(", ", locals)).AppendLine("];");
                statements.Append("var ").Append(builder).Append(" = JsonElement.CreateBuilder(").Append(workspaceVariable).AppendLine(",");
                statements.Append("    (ReadOnlySpan<JsonElement>)").Append(builder).AppendLine("Values,");
                statements.AppendLine("    static (in ReadOnlySpan<JsonElement> values, ref JsonElement.ObjectBuilder builder) =>");
                statements.AppendLine("    {");
                for (int i = 0; i < names.Count; i++)
                {
                    statements.Append("        builder.AddProperty(").Append(EmitText.Quote(names[i])).Append("u8, values[")
                        .Append(i.ToString(CultureInfo.InvariantCulture)).AppendLine("]);");
                }

                statements.AppendLine("    });");
                return $"{builder}.RootElement";
            }

            case Stj.JsonValueKind.Array:
            {
                var locals = new List<string>();
                int index = 0;
                foreach (Stj.JsonElement item in template.EnumerateArray())
                {
                    locals.Add(EmitValue(stepId, item, workspaceVariable, sources, inputsVariable, stepOutputLocals, inputAccessors, fields, statements, $"{baseName}_{index.ToString(CultureInfo.InvariantCulture)}"));
                    index++;
                }

                string builder = $"{baseName}Arr";
                statements.Append("Span<JsonElement> ").Append(builder).Append("Values = [").Append(string.Join(", ", locals)).AppendLine("];");
                statements.Append("var ").Append(builder).Append(" = JsonElement.CreateBuilder(").Append(workspaceVariable).AppendLine(",");
                statements.Append("    (ReadOnlySpan<JsonElement>)").Append(builder).AppendLine("Values,");
                statements.AppendLine("    static (in ReadOnlySpan<JsonElement> values, ref JsonElement.ArrayBuilder builder) =>");
                statements.AppendLine("    {");
                for (int i = 0; i < locals.Count; i++)
                {
                    statements.Append("        builder.AddItem(values[").Append(i.ToString(CultureInfo.InvariantCulture)).AppendLine("]);");
                }

                statements.AppendLine("    });");
                return $"{builder}.RootElement";
            }

            case Stj.JsonValueKind.String:
            {
                string value = template.GetString()!;
                if (value.Contains("{$", StringComparison.Ordinal))
                {
                    return EmitInterpolation(stepId, value, workspaceVariable, sources, inputsVariable, stepOutputLocals, inputAccessors, statements, baseName);
                }

                if (value.StartsWith('$'))
                {
                    (ArazzoExpression expression, string? navigationPointer) = CriterionExpressionParsing.SplitNavigation(value);

                    // A leading-'$' string that matches a recognized runtime-expression source must resolve;
                    // one that matches none is a literal (no '$' escape exists, and the runtime parser treats
                    // an unrecognized '$'-form as a literal) — fall through to the constant bake below.
                    if (expression.Source != ArazzoExpressionSource.Literal)
                    {
                        if (CriterionExpressionParsing.TryEmitElementNavigation(
                                expression, navigationPointer, $"{baseName}E", sources, inputsVariable, stepOutputLocals, inputAccessors, statements, out string elementLocal))
                        {
                            return elementLocal;
                        }

                        throw new NotSupportedException(
                            $"Request/reply receive step '{stepId}' has a reply value '{value}' that cannot be resolved; a responder reply may reference only $message, $inputs, and $steps.");
                    }
                }

                // A constant string (plain, or a leading-'$' form that is not a runtime expression).
                return EmitConstant(raw, fields, $"{baseName}Const");
            }

            default:
                // Number/boolean/null never contain '$', so they were handled by the constant fast path.
                return EmitConstant(raw, fields, $"{baseName}Const");
        }
    }
}