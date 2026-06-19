// <copyright file="InterpolationEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits the code that resolves an interpolated string value — a template with embedded
/// <c>{$expression}</c> fragments (for example <c>"Bearer {$steps.login.outputs.token}"</c>) — to a
/// <see cref="JsonElement"/> string local (plan §3.1).
/// </summary>
/// <remarks>
/// The template is compiled once into a <c>static readonly</c>
/// <see cref="CompiledInterpolationTemplate"/> field; at run time the context interpolates it to a
/// UTF-8 buffer and the result is wrapped as a JSON string. Interpolation always yields text, so the
/// value is a JSON string regardless of the embedded expressions' types.
/// <para><strong>Allocation ledger.</strong> The interpolation buffer is rented from the per-call
/// <c>workspace</c> pool (<c>RentWriterAndBuffer</c>), not <c>new</c>-ed per step. Because
/// <c>FixedJsonValueDocument.ForUnescapedString</c> <em>copies</em> the bytes into the result document
/// (it does not reference the source span), the pooled buffer is returned <strong>synchronously</strong>
/// in the emitted statements — before any downstream <c>await</c> — so the thread-affine pooled writer
/// is never held across an await (issue #814). This is the fallback the <see cref="InterpolationInliner"/>
/// defers to for non-statically-navigable fragments (<c>$url</c>, <c>$method</c>, headers, …); those are
/// common in real workflows, so the per-step heap <c>ArrayBufferWriter</c> it replaced was on the hot path.</para>
/// </remarks>
internal static class InterpolationEmitter
{
    /// <summary>
    /// Emits the static template field and the in-method statements that assign a
    /// <see cref="JsonElement"/> string named <paramref name="resultLocal"/> from an interpolated template.
    /// </summary>
    /// <param name="fields">Accumulates <c>static readonly</c> field declarations.</param>
    /// <param name="statements">Accumulates the in-method statements.</param>
    /// <param name="template">The interpolation template (e.g. <c>"order-{$inputs.id}"</c>).</param>
    /// <param name="resultLocal">The name of the <see cref="JsonElement"/> local to assign.</param>
    /// <param name="contextVariable">The in-scope <c>WorkflowExecutionContext</c> variable name.</param>
    /// <param name="fieldName">The unique name for the compiled-template field.</param>
    public static void Emit(
        StringBuilder fields,
        StringBuilder statements,
        string template,
        string resultLocal,
        string contextVariable,
        string fieldName)
    {
        fields.Append("private static readonly CompiledInterpolationTemplate ").Append(fieldName)
            .Append(" = CompiledInterpolationTemplate.Compile(").Append(EmitText.Quote(template)).AppendLine(");");

        string writer = $"{resultLocal}Writer";
        string buffer = $"{resultLocal}Buffer";

        // Rent the interpolation buffer from the workspace pool (not a per-step `new ArrayBufferWriter`). TryInterpolate
        // writes the UTF-8 result into it; ForUnescapedString then copies those bytes into the result document, so the
        // pooled buffer is returned right here — synchronously, before any downstream await — keeping the thread-affine
        // writer off the await path (#814). The resulting JsonElement carries its own copy and outlives the return.
        statements.Append("var ").Append(writer).Append(" = workspace.RentWriterAndBuffer(256, out IByteBufferWriter ").Append(buffer).AppendLine(");");
        statements.Append("_ = ").Append(contextVariable).Append(".TryInterpolate(").Append(fieldName).Append(", ").Append(buffer).AppendLine(");");
        statements.Append("JsonElement ").Append(resultLocal)
            .Append(" = Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement>.ForUnescapedString(")
            .Append(buffer).AppendLine(".WrittenSpan).RootElement;");
        statements.Append("workspace.ReturnWriterAndBuffer(").Append(writer).Append(", ").Append(buffer).AppendLine(");");
    }
}