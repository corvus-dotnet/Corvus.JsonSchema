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
/// <see cref="Interpolation.AppendValue"/> helper, all into a workspace-pooled buffer. The buffer's
/// <c>WrittenSpan</c> is passed straight to the client method as its <c>Source</c> (the generated
/// client materialises it synchronously, exactly like a literal <c>u8</c> argument), so there is no
/// <c>WorkflowExecutionContext</c>, no <see cref="CompiledInterpolationTemplate"/> field, and no
/// reified string document — only the one copy the client makes into its own workspace. The pooled
/// buffer is returned after the call via the emitted <see cref="InterpolationInlineCode.Cleanup"/>.
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
    /// <param name="responseBodyLocal">The in-scope live response-body local, or <see langword="null"/> when no body is bound (request-side interpolation has none).</param>
    /// <param name="inputsVariable">The in-scope workflow inputs variable.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputAccessors">Map of input JSON name → generated dotnet accessor, or <see langword="null"/> for untyped inputs.</param>
    /// <param name="tmpPrefix">A unique prefix for the emitted temporaries.</param>
    /// <param name="code">When this method returns <see langword="true"/>, the statements, the <c>Source</c> expression, and the post-call cleanup.</param>
    /// <returns><see langword="true"/> if the template was inlined; otherwise <see langword="false"/> (use the fallback).</returns>
    public static bool TryEmit(
        string template,
        string? responseBodyLocal,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        IReadOnlyDictionary<string, string>? inputAccessors,
        string tmpPrefix,
        out InterpolationInlineCode code)
    {
        code = default;

        string writer = $"{tmpPrefix}Writer";
        string buffer = $"{tmpPrefix}Buffer";

        // Emit the rent + each segment write; bail (→ fallback) on any non-navigable fragment.
        var statements = new StringBuilder();
        statements.Append("var ").Append(writer).Append(" = workspace.RentWriterAndBuffer(256, out IByteBufferWriter ").Append(buffer).AppendLine(");");

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
                    EmitLiteral(statements, buffer, span[i..]);
                    i = span.Length;
                    break;
                }

                int closeIndex = i + relativeClose;
                string expressionText = span[(i + 1)..closeIndex].ToString();
                ArazzoExpression expression = ArazzoExpression.Parse(expressionText);

                var navigation = new StringBuilder();
                if (!CriterionExpressionParsing.TryEmitElementNavigation(
                    expression, null, $"{tmpPrefix}Seg{segment}", new CriterionSources(responseBodyLocal), inputsVariable, stepOutputLocals, inputAccessors, navigation, out string segmentLocal))
                {
                    return false;
                }

                statements.Append(navigation);
                statements.Append("Interpolation.AppendValue(").Append(buffer).Append(", ").Append(segmentLocal).AppendLine(");");
                segment++;
                i = closeIndex + 1;
                continue;
            }

            int next = IndexOfEmbeddedStart(span, i);
            if (next < 0)
            {
                EmitLiteral(statements, buffer, span[i..]);
                break;
            }

            EmitLiteral(statements, buffer, span[i..next]);
            i = next;
        }

        // The interpolated UTF-8 is handed to the client as its Source span (the client copies it into
        // its own workspace synchronously), and the pooled buffer is returned after the call.
        code = new InterpolationInlineCode(
            statements.ToString(),
            $"{buffer}.WrittenSpan",
            $"workspace.ReturnWriterAndBuffer({writer}, {buffer});\n");
        return true;
    }

    private static void EmitLiteral(StringBuilder statements, string buffer, ReadOnlySpan<char> literal)
    {
        if (literal.IsEmpty)
        {
            return;
        }

        statements.Append("Interpolation.AppendUtf8(").Append(buffer).Append(", ").Append(EmitText.Quote(literal.ToString())).AppendLine("u8);");
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

/// <summary>
/// The code emitted for an inlined interpolation.
/// </summary>
/// <param name="Statements">The statements that rent the pooled buffer and write each segment, emitted before the client call.</param>
/// <param name="SourceExpression">The <c>Source</c> expression (the buffer's <c>WrittenSpan</c>) to pass as the client argument.</param>
/// <param name="Cleanup">The statement that returns the pooled buffer, emitted after the client call.</param>
internal readonly record struct InterpolationInlineCode(string Statements, string SourceExpression, string Cleanup);