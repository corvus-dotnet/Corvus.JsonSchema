// <copyright file="Interpolation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text.Json;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Runtime helpers that generated executors call when inlining an interpolated string value — a
/// template with embedded <c>{$expression}</c> fragments (for example <c>"pet-{$inputs.id}"</c>).
/// </summary>
/// <remarks>
/// The generator emits each literal segment as a UTF-8 write and each embedded value via
/// <see cref="AppendValue"/>, building the result directly in a pooled buffer — with no
/// <see cref="WorkflowExecutionContext"/>, no <see cref="CompiledInterpolationTemplate"/>, and no
/// per-call template parse. <see cref="AppendValue"/> mirrors the runtime interpreter's
/// <c>AppendJsonEmbedded</c> so inlined and interpreted interpolation produce identical text.
/// </remarks>
public static class Interpolation
{
    /// <summary>
    /// Appends a literal UTF-8 segment to an interpolation buffer.
    /// </summary>
    /// <param name="output">The UTF-8 buffer receiving the text.</param>
    /// <param name="utf8">The literal UTF-8 bytes.</param>
    /// <remarks>
    /// Writes via an explicitly-sized <see cref="IBufferWriter{T}.GetSpan"/> rather than
    /// <see cref="System.Buffers.BuffersExtensions"/>, whose <c>Write</c> requests a zero-size span —
    /// rejected by the pooled buffer the generator rents.
    /// </remarks>
    public static void AppendUtf8(IBufferWriter<byte> output, ReadOnlySpan<byte> utf8)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (utf8.IsEmpty)
        {
            return;
        }

        Span<byte> destination = output.GetSpan(utf8.Length);
        utf8.CopyTo(destination);
        output.Advance(utf8.Length);
    }

    /// <summary>
    /// Appends an embedded value to an interpolation buffer using the Arazzo rules: a JSON string is
    /// written as its unescaped UTF-8 text (no surrounding quotes); any other value (number, boolean,
    /// null, object, or array) is written as RFC 8259 JSON. An undefined value (an expression that did
    /// not resolve) contributes nothing, matching the interpreter, which skips an unresolved fragment.
    /// </summary>
    /// <param name="output">The UTF-8 buffer receiving the interpolated text.</param>
    /// <param name="value">The resolved embedded value.</param>
    public static void AppendValue(IBufferWriter<byte> output, in JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(output);

        switch (value.ValueKind)
        {
            case JsonValueKind.Undefined:
                return;

            case JsonValueKind.String:
                using (UnescapedUtf8JsonString unescaped = value.GetUtf8String())
                {
                    AppendUtf8(output, unescaped.Span);
                }

                return;

            default:
                using (var writer = new Utf8JsonWriter(output))
                {
                    value.WriteTo(writer);
                }

                return;
        }
    }
}