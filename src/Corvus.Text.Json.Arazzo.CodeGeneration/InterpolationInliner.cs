// <copyright file="InterpolationInliner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Inlines an interpolated string value — a template with embedded <c>{$expression}</c> fragments
/// (for example <c>"pet-{$inputs.id}"</c>) — by emitting each literal segment as a UTF-8 write and
/// each embedded expression as a statically-navigated value appended via the runtime
/// <see cref="Interpolation.AppendValue"/> helper, all into a workspace-pooled buffer. The result is
/// a workspace-registered string document, so there is no <c>WorkflowExecutionContext</c>, no
/// <see cref="CompiledInterpolationTemplate"/> field, and no per-call throwaway buffer.
/// </summary>
/// <remarks>
/// Falls back to the context-based <see cref="InterpolationEmitter"/> when any embedded expression is
/// not statically navigable (<c>$url</c>, <c>$method</c>, <c>$request.*</c>, a header, …). Mirrors the
/// runtime <c>TryInterpolateCore</c> scanner: a fragment begins with <c>{$</c> and ends at the next
/// <c>}</c>; a bare <c>{</c> is literal text.
/// </remarks>
internal static class InterpolationInliner
{
    /// <summary>
    /// Attempts to inline an interpolation template.
    /// </summary>
    /// <param name="template">The interpolation template (e.g. <c>"order-{$inputs.id}"</c>).</param>
    /// <param name="resultLocal">The name of the <see cref="JsonElement"/> local to assign.</param>
    /// <param name="responseBodyLocal">The in-scope live response-body local, or <see langword="null"/> when no body is bound (request-side interpolation has none).</param>
    /// <param name="inputsVariable">The in-scope workflow inputs variable.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputAccessors">Map of input JSON name → generated dotnet accessor, or <see langword="null"/> for untyped inputs.</param>
    /// <param name="tmpPrefix">A unique prefix for the emitted temporaries.</param>
    /// <param name="statements">When this method returns <see langword="true"/>, the statements that assign <paramref name="resultLocal"/>.</param>
    /// <returns><see langword="true"/> if the template was inlined; otherwise <see langword="false"/> (use the fallback).</returns>
    public static bool TryEmit(
        string template,
        string resultLocal,
        string? responseBodyLocal,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        string tmpPrefix,
        out string statements)
    {
        statements = string.Empty;

        string writer = $"{tmpPrefix}Writer";
        string buffer = $"{tmpPrefix}Buffer";
        string doc = $"{tmpPrefix}Doc";

        // Emit each segment into the body of the try; bail (→ fallback) on any non-navigable fragment.
        var inner = new StringBuilder();
        int segment = 0;
        ReadOnlySpan<char> span = template;
        int i = 0;
        while (i < span.Length)
        {
            if (span[i] == '{' && i + 1 < span.Length && span[i + 1] == '$')
            {
                int relativeClose = span[i..].IndexOf('}');
                if (relativeClose < 0)
                {
                    // No closing brace — the remainder is literal text, matching the interpreter.
                    EmitLiteral(inner, buffer, span[i..]);
                    i = span.Length;
                    break;
                }

                int closeIndex = i + relativeClose;
                string expressionText = span[(i + 1)..closeIndex].ToString();
                ArazzoExpression expression = ArazzoExpression.Parse(expressionText);

                var navigation = new StringBuilder();
                if (!CriterionExpressionParsing.TryEmitElementNavigation(
                    expression, null, $"{tmpPrefix}Seg{segment}", responseBodyLocal, inputsVariable, stepOutputLocals, inputAccessors, navigation, out string segmentLocal))
                {
                    return false;
                }

                inner.Append(navigation);
                inner.Append("    Interpolation.AppendValue(").Append(buffer).Append(", ").Append(segmentLocal).AppendLine(");");
                segment++;
                i = closeIndex + 1;
                continue;
            }

            int next = IndexOfEmbeddedStart(span, i);
            if (next < 0)
            {
                EmitLiteral(inner, buffer, span[i..]);
                break;
            }

            EmitLiteral(inner, buffer, span[i..next]);
            i = next;
        }

        var body = new StringBuilder();
        body.Append("JsonElement ").Append(resultLocal).AppendLine(" = default;");
        body.Append("var ").Append(writer).Append(" = workspace.RentWriterAndBuffer(256, out IByteBufferWriter ").Append(buffer).AppendLine(");");
        body.AppendLine("try");
        body.AppendLine("{");
        body.Append(inner);
        body.Append("    var ").Append(doc)
            .Append(" = Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement>.ForUnescapedString(").Append(buffer).AppendLine(".WrittenSpan);");
        body.Append("    workspace.RegisterDocument(").Append(doc).AppendLine(");");
        body.Append("    ").Append(resultLocal).Append(" = ").Append(doc).AppendLine(".RootElement;");
        body.AppendLine("}");
        body.AppendLine("finally");
        body.AppendLine("{");
        body.Append("    workspace.ReturnWriterAndBuffer(").Append(writer).Append(", ").Append(buffer).AppendLine(");");
        body.AppendLine("}");

        statements = body.ToString();
        return true;
    }

    private static void EmitLiteral(StringBuilder inner, string buffer, ReadOnlySpan<char> literal)
    {
        if (literal.IsEmpty)
        {
            return;
        }

        inner.Append("    Interpolation.AppendUtf8(").Append(buffer).Append(", ").Append(EmitText.Quote(literal.ToString())).AppendLine("u8);");
    }

    private static int IndexOfEmbeddedStart(ReadOnlySpan<char> span, int from)
    {
        for (int j = from; j < span.Length - 1; j++)
        {
            if (span[j] == '{' && span[j + 1] == '$')
            {
                return j;
            }
        }

        return -1;
    }
}