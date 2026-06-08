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

        string buffer = $"{resultLocal}Buffer";
        statements.Append("var ").Append(buffer).AppendLine(" = new System.Buffers.ArrayBufferWriter<byte>();");
        statements.Append("_ = ").Append(contextVariable).Append(".TryInterpolate(").Append(fieldName).Append(", ").Append(buffer).AppendLine(");");
        statements.Append("JsonElement ").Append(resultLocal)
            .Append(" = Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement>.ForUnescapedString(")
            .Append(buffer).AppendLine(".WrittenSpan).RootElement;");
    }
}